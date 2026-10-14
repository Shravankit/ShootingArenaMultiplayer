"use strict";

const crypto = require("crypto");
const { WebSocket } = require("ws");

const now = () => Date.now();

const rid = (bytes) => crypto.randomBytes(bytes).toString("hex");

const isPlainObject = (v) =>
  v !== null && typeof v === "object" && !Array.isArray(v);

const cleanStr = (v, max, fallback = "") =>
  typeof v === "string" && v.trim() ? v.trim().slice(0, max) : fallback;

function rawSend(ws, msg) {
  if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
}

module.exports = { now, rid, isPlainObject, cleanStr, rawSend };
