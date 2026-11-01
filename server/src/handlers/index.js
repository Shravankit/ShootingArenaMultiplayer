"use strict";

const roomHandlers = require("./roomHandlers");
const syncHandlers = require("./syncHandlers");
const combatHandlers = require("./combatHandlers");

const handlers = {
  ...roomHandlers,
  ...syncHandlers,
  ...combatHandlers,
};

function dispatch(s, m) {
  if (!Object.hasOwn(handlers, m.t)) {
    return s.send({
      t: "error",
      code: "UNKNOWN_TYPE",
      message: m.t,
    });
  }

  try {
    handlers[m.t](s, m);
  } catch (e) {
    console.error(`handler ${m.t} failed:`, e);
    s.send({
      t: "error",
      code: "INTERNAL",
      message: "Server error",
    });
  }
}

module.exports = { handlers, dispatch };
