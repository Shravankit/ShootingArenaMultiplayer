"use strict";

const CONFIG = require("./config");
const { rooms } = require("./Store");

function startLoops(wss) {
  // Heartbeat: detect half-open sockets so reconnect grace starts promptly.
  const heartbeat = setInterval(() => {
    for (const ws of wss.clients) {
      if (!ws.isAlive) {
        ws.terminate();
        continue;
      }
      ws.isAlive = false;
      ws.ping();
    }
  }, CONFIG.heartbeatMs);

  // Snapshot loop: deltas every tick, full snapshot every few seconds.
  const fullEvery = Math.max(
    1,
    Math.round(CONFIG.tickRate * CONFIG.fullSnapshotEverySec),
  );
  const ticker = setInterval(() => {
    for (const room of rooms.values()) {
      room.tick++;
      const full = room.tick % fullEvery === 0;
      if (full || room.dirty.size > 0) {
        room.broadcast(room.snapshot(full));
        room.dirty.clear();
      }
    }
  }, 1000 / CONFIG.tickRate);

  return function stopLoops() {
    clearInterval(heartbeat);
    clearInterval(ticker);
  };
}

module.exports = { startLoops };
