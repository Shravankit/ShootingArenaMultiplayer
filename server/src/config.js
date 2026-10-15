"use strict";

module.exports = {
  port: Number(process.env.PORT) || 8080,
  tickRate: Number(process.env.TICK_RATE) || 20, // snapshots per second
  fullSnapshotEverySec: 5, // drift correction
  matchSize: Number(process.env.MATCH_SIZE) || 4,
  reconnectGraceMs: Number(process.env.RECONNECT_GRACE_MS) || 30000,
  heartbeatMs: 10000,
  maxPlayersPerRoom: 16,
  maxMsgPerSec: 120,
  maxPayloadBytes: 64 * 1024,
  maxRoomStateBytes: 16 * 1024,
};
