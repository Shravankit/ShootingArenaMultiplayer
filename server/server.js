"use strict";

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

  if (req.url === "/rooms") {
    res.writeHead(200, { "Content-Type": "application/json" });
    return res.end(
      JSON.stringify(
        [...rooms.values()].map((r) => ({
          id: r.id,
          phase: r.roomState.phase || "lobby",
          players: [...r.members.values()].map((p) => ({
            id: p.id,
            name: p.name,
            connected: p.connected,
            ready: p.state?.ready === true,
          })),
        })),
        null,
        2,
      ),
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
