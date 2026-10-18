"use strict";

const { rid, rawSend } = require("./Utils");

class Session {
  constructor(name) {
    this.id = rid(4); // public player id
    this.token = rid(24); // private resume token
    this.name = name;
    this.ws = null;
    this.connected = false;
    this.roomId = null;
    this.state = null; // latest player state blob (client-defined)
    this.stateSeq = 0;
    this.graceTimer = null;
  }

  send(msg) {
    if (this.connected) rawSend(this.ws, msg);
  }

  pub() {
    return { id: this.id, name: this.name, connected: this.connected };
  }
}

module.exports = Session;
