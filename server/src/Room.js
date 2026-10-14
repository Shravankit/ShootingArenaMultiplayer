"use strict";

const crypto = require("crypto");
const { rooms } = require("./Store");
const { now } = require("./Utils");

function newRoomId() {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

  for (;;) {
    const b = crypto.randomBytes(5);
    let id = "";
    for (let i = 0; i < 5; i++) id += chars[b[i] % chars.length];
    if (!rooms.has(id)) return id;
  }
}

class Room {
  constructor(opts, host) {
    this.id = newRoomId();
    this.name = opts.name;
    this.maxPlayers = opts.maxPlayers;
    this.password = opts.password;
    this.isPrivate = opts.isPrivate;
    this.hostId = host.id;
    this.members = new Map(); // playerId -> Session
    this.roomState = opts.roomState; // host-owned shared key/value state
    this.version = 0; // bumps whenever roomState changes
    this.tick = 0;
    this.dirty = new Set(); // playerIds whose state changed since last tick
    this.createdAt = now();
  }

  info() {
    return {
      id: this.id,
      name: this.name,
      players: this.members.size,
      maxPlayers: this.maxPlayers,
      hasPassword: !!this.password,
      hostId: this.hostId,
    };
  }

  broadcast(msg, exceptId = null) {
    for (const p of this.members.values()) {
      if (p.id !== exceptId) p.send(msg);
    }
  }

  snapshot(full) {
    const list = [];
    const source = full
      ? this.members.values()
      : [...this.dirty].map((id) => this.members.get(id));
    for (const p of source) {
      if (!p || !p.state) continue;
      list.push({
        id: p.id,
        seq: p.stateSeq,
        connected: p.connected,
        data: p.state,
      });
    }
    return {
      t: "snapshot",
      full,
      tick: this.tick,
      time: now(),
      roomVersion: this.version,
      players: list,
    };
  }

  // Everything a client needs to (re)build the world: used on join and on resume.
  joinedPayload(selfSession, resumed) {
    return {
      t: "room_joined",
      resumed,
      selfId: selfSession.id,
      room: this.info(),
      players: [...this.members.values()].map((p) => p.pub()),
      roomState: this.roomState,
      roomVersion: this.version,
      snapshot: this.snapshot(true),
    };
  }
}

module.exports = Room;
