"use strict";

const CONFIG = require("../config");
const Room = require("../Room");
const { rooms } = require("../store");
const { joinRoom, removeFromRoom } = require("../roomOps");
const { isPlainObject, cleanStr } = require("../utils");

const roomHandlers = {
  list_rooms(s) {
    const list = [...rooms.values()]
      .filter((r) => !r.isPrivate)
      .map((r) => r.info());
    s.send({ t: "room_list", rooms: list });
  },

  create_room(s, m) {
    if (s.roomId)
      return s.send({
        t: "error",
        code: "ALREADY_IN_ROOM",
        message: "Leave your room first",
      });

    const roomState = isPlainObject(m.roomState) ? m.roomState : {};
    if (JSON.stringify(roomState).length > CONFIG.maxRoomStateBytes) {
      return s.send({
        t: "error",
        code: "ROOM_STATE_TOO_LARGE",
        message: "roomState too large",
      });
    }
    const maxPlayers = Math.min(
      Math.max(Number(m.maxPlayers) || 8, 2),
      CONFIG.maxPlayersPerRoom,
    );
    const room = new Room(
      {
        name: cleanStr(m.name, 32, `${s.name}'s room`),
        maxPlayers,
        password: cleanStr(m.password, 32),
        isPrivate: !!m.isPrivate,
        roomState,
      },
      s,
    );
    rooms.set(room.id, room);
    joinRoom(s, room);
  },

  join_room(s, m) {
    if (s.roomId)
      return s.send({
        t: "error",
        code: "ALREADY_IN_ROOM",
        message: "Leave your room first",
      });

    const room = rooms.get(cleanStr(m.roomId, 8).toUpperCase());
    if (!room)
      return s.send({
        t: "error",
        code: "ROOM_NOT_FOUND",
        message: "Room not found",
      });
    if (room.members.size >= room.maxPlayers)
      return s.send({ t: "error", code: "ROOM_FULL", message: "Room is full" });
    if (room.password && room.password !== cleanStr(m.password, 32)) {
      return s.send({
        t: "error",
        code: "BAD_PASSWORD",
        message: "Wrong password",
      });
    }
    joinRoom(s, room);
  },

  quick_join(s) {
    if (s.roomId)
      return s.send({
        t: "error",
        code: "ALREADY_IN_ROOM",
        message: "Leave your room first",
      });

    const open = [...rooms.values()].find(
      (r) => !r.isPrivate && !r.password && r.members.size < r.maxPlayers,
    );
    if (open) return joinRoom(s, open);
    roomHandlers.create_room(s, { name: "Quick match", maxPlayers: 8 });
  },

  leave_room(s) {
    if (!s.roomId) return;
    removeFromRoom(s, "left");
    s.send({ t: "room_left" });
  },

  kick(s, m) {
    const room = rooms.get(s.roomId);
    if (!room) return;
    if (room.hostId !== s.id)
      return s.send({
        t: "error",
        code: "NOT_HOST",
        message: "Only the host can do that",
      });
    const target = room.members.get(String(m.playerId));
    if (!target || target.id === s.id) return;
    target.send({ t: "kicked" });
    removeFromRoom(target, "kicked");
  },
};

module.exports = roomHandlers;
