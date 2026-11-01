"use strict";

const CombatService = require("../combat/CombatService");

module.exports = {
  fire_request(session, message) {
    CombatService.handleFire(session, message);
  },

  reload_request(session) {
    CombatService.handleReload(session);
  },
};
