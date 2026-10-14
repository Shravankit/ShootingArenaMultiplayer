"use strict";

const CONFIG = require("./config");
const Session = require("./Session");
const { sessions, rooms } = require("./Store");
const { destroySession } = require("./Roomops");
const { dispatch } = require("./handlers");
const { now, rid, isPlainObject, cleanStr, rawSend } = require("./Utils");

function onHello(ws, m) {
  if (ws.session) return; // already identified

  let s = typeof m.token === "string" ? sessions.get(m.token) : null;
  const resumed = !!s;

  if (s) {
    // Kick a zombie socket that hasn't been noticed as dead yet.
    if (s.ws && s.ws !== ws) {
      const old = s.ws;
      old.session = null; // so its close handler becomes a no-op
      old.close(4000, "replaced");
    }
    clearTimeout(s.graceTimer);
    s.graceTimer = null;
  } else {
    s = new Session(cleanStr(m.name, 24, `Player-${rid(2)}`));
    sessions.set(s.token, s);
  }

  if (resumed && m.name) s.name = cleanStr(m.name, 24, s.name);

  s.ws = ws;
  ws.session = s;
  s.connected = true;

  s.send({
    t: "welcome",
    id: s.id,
    token: s.token,
    resumed,
    serverTime: now(),
    tickRate: CONFIG.tickRate,
    graceMs: CONFIG.reconnectGraceMs,
  });

  if (resumed && s.roomId) {
    const room = rooms.get(s.roomId);
    if (room) {
      s.send(room.joinedPayload(s, true)); // full world snapshot
      room.broadcast({ t: "player_reconnected", id: s.id }, s.id);
    } else {
      s.roomId = null;
    }
  }
}

function onClose(ws) {
  const s = ws.session;
  if (!s || s.ws !== ws) return; // replaced by a newer socket
  s.connected = false;
  s.ws = null;

  const room = rooms.get(s.roomId);
  if (room) {
    room.dirty.add(s.id);
    room.broadcast(
      { t: "player_disconnected", id: s.id, graceMs: CONFIG.reconnectGraceMs },
      s.id,
    );
  }
  s.graceTimer = setTimeout(
    () => destroySession(s, "timeout"),
    CONFIG.reconnectGraceMs,
  );
}

function onMessage(ws, raw, isBinary) {
  ws.isAlive = true;
  if (isBinary) return;

  // rate limit
  const t = now();
  if (t - ws.winStart >= 1000) {
    ws.winStart = t;
    ws.winCount = 0;
  }
  if (++ws.winCount > CONFIG.maxMsgPerSec) {
    if (ws.winCount > CONFIG.maxMsgPerSec * 3) ws.close(1008, "rate limit");
    return;
  }

  let m;
  try {
    m = JSON.parse(raw.toString());
  } catch {
    return;
  }
  if (!isPlainObject(m) || typeof m.t !== "string") return;

  if (m.t === "hello") return onHello(ws, m);

  const s = ws.session;
  if (!s)
    return rawSend(ws, {
      t: "error",
      code: "NO_HELLO",
      message: "Send hello first",
    });

  dispatch(s, m);
}

function attachConnectionHandlers(wss) {
  wss.on("connection", (ws) => {
    ws.session = null;
    ws.isAlive = true;
    ws.winStart = now();
    ws.winCount = 0;

    ws.on("pong", () => {
      ws.isAlive = true;
    });
    ws.on("error", (e) => console.warn("socket error:", e.message));
    ws.on("close", () => onClose(ws));
    ws.on("message", (raw, isBinary) => onMessage(ws, raw, isBinary));
  });
}

module.exports = { attachConnectionHandlers };
