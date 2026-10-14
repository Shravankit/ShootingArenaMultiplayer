"use strict";

const CONFIG = require("../../config");
const { rooms } = require("../../store");
const { now, isPlainObject, cleanStr } = require("../../utils");

module.exports = {
  ping(s, m) {
    s.send({ t: "pong", ct: m.ct, st: now() });
  },

  // Client sends its whole state blob (position, rotation, anim params, ...).
  // Server stores it and ships it out in the next tick's snapshot.
  state(s, m) {
    const room = rooms.get(s.roomId);
    if (!room || !isPlainObject(m.data)) return;
    const seq = Number.isInteger(m.seq) ? m.seq : s.stateSeq + 1;
    if (seq <= s.stateSeq) return; // stale / out of order
    s.stateSeq = seq;
    s.state = m.data;
    room.dirty.add(s.id);
  },

  // Reliable relay for one-off things: shoot, hit, pickup, chat...
  event(s, m) {
    const room = rooms.get(s.roomId);
    if (!room) return;
    const msg = {
      t: "event",
      from: s.id,
      name: cleanStr(m.name, 48),
      data: m.data ?? null,
      time: now(),
    };
    if (!msg.name) return;
    if (m.to) {
      const target = room.members.get(String(m.to));
      if (target) target.send(msg);
    } else {
      room.broadcast(msg, m.echo ? null : s.id);
    }
  },

  // Host-owned shared state (match phase, scores, map choice...). null value deletes a key.
  set_room_state(s, m) {
    const room = rooms.get(s.roomId);
    if (!room) return;
    if (room.hostId !== s.id)
      return s.send({
        t: "error",
        code: "NOT_HOST",
        message: "Only the host can do that",
      });
    if (!isPlainObject(m.patch)) return;

    const next = { ...room.roomState };
    for (const [k, v] of Object.entries(m.patch)) {
      if (v === null) delete next[k];
      else next[k] = v;
    }
    if (JSON.stringify(next).length > CONFIG.maxRoomStateBytes) {
      return s.send({
        t: "error",
        code: "ROOM_STATE_TOO_LARGE",
        message: "roomState too large",
      });
    }
    room.roomState = next;
    room.version++;
    room.broadcast({ t: "room_state", version: room.version, patch: m.patch });
  },

  // Client noticed a gap / desync: give it everything again.
  request_snapshot(s) {
    const room = rooms.get(s.roomId);
    if (room) s.send(room.snapshot(true));
  },
};
