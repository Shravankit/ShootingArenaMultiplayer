"use strict";

/**
 * Unity multiplayer server (ws) - entry point.
 *
 * Layout
 *   src/config.js              tunables (env overridable)
 *   src/utils.js               small helpers
 *   src/store.js               in-memory sessions + rooms
 *   src/Session.js             player session (survives socket drops)
 *   src/Room.js                room, snapshots, join payload
 *   src/roomOps.js             join / leave / destroy session
 *   src/handlers/              message handlers by area (rooms, sync) + dispatcher
 *   src/connection.js          hello/resume, close, rate limit, message routing
 *   src/loops.js               heartbeat + snapshot tick
 *
 * Protocol: JSON text frames, every message has a string field "t" (type).
 */

const http = require("http");
const { WebSocketServer } = require("ws");

const CONFIG = require("./src/config");
const { rooms, sessions } = require("./src/Store");
const { attachConnectionHandlers } = require("./src/Connection");
const { startLoops } = require("./src/loops");

const httpServer = http.createServer((req, res) => {
  if (req.url === "/health") {
    res.writeHead(200, { "Content-Type": "application/json" });
    return res.end(
      JSON.stringify({
        ok: true,
        rooms: rooms.size,
        sessions: sessions.size,
        uptime: process.uptime(),
      }),
    );
  }
  res.writeHead(404);
  res.end();
});

const wss = new WebSocketServer({
  server: httpServer,
  maxPayload: CONFIG.maxPayloadBytes,
});
attachConnectionHandlers(wss);
const stopLoops = startLoops(wss);

httpServer.listen(CONFIG.port, () => {
  console.log(
    `WS multiplayer server on :${CONFIG.port}  (tick ${CONFIG.tickRate} Hz, grace ${CONFIG.reconnectGraceMs} ms)`,
  );
});

function shutdown() {
  console.log("Shutting down...");
  stopLoops();
  for (const ws of wss.clients) ws.close(1001, "server shutdown");
  httpServer.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 2000).unref();
}
process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
