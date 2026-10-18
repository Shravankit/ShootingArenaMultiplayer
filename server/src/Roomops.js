"use strict";

const { sessions, rooms } = require("./Store");

function joinRoom(s, room) {
  room.members.set(s.id, s);
  s.roomId = room.id;
  s.state = null;
  s.stateSeq = 0;
  s.send(room.joinedPayload(s, false));
  room.broadcast({ t: "player_joined", player: s.pub() }, s.id);

  console.log(
    `[room ${room.id}] ${s.name} (${s.id}) joined -> ` +
      `${room.members.size}/${room.maxPlayers}: ` +
      [...room.members.values()].map((p) => p.name).join(", "),
  ); // for testing
}

function removeFromRoom(s, reason) {
  const room = rooms.get(s.roomId);
  s.roomId = null;
  s.state = null;
  if (!room) return;

  room.members.delete(s.id);
  console.log(
    `[room ${room.id}] ${s.name} (${s.id}) left (${reason}) -> ${room.members.size} remaining`,
  ); //for testing
  room.dirty.delete(s.id);

  if (room.members.size === 0) {
    rooms.delete(room.id);
    return;
  }

  room.broadcast({ t: "player_left", id: s.id, reason });

  if (room.hostId === s.id) {
    const next =
      [...room.members.values()].find((p) => p.connected) ||
      room.members.values().next().value;
    room.hostId = next.id;
    room.broadcast({ t: "host_changed", hostId: next.id });
  }
}

function destroySession(s, reason) {
  clearTimeout(s.graceTimer);
  removeFromRoom(s, reason);
  sessions.delete(s.token);
}

module.exports = { joinRoom, removeFromRoom, destroySession };
