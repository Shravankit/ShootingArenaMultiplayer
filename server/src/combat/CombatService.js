"use strict";

const { rooms } = require("../Store");
const WEAPONS = require("./WeaponConfig");

const MAX_HEALTH = 100;
const RESPAWN_DELAY_MS = 3000;

// Player hitbox: capsule of height 2, radius 0.5 (matches the prefab).
// state.x/y/z is the capsule center, so the axis runs 0.5 below to 0.5 above it.
const PLAYER_HALF_SEGMENT = 0.5;
const PLAYER_RADIUS = 0.5;
const MAX_ORIGIN_OFFSET = 3; // camera sits ~3.5m behind the player

const players = new Map();

// ---------- vector helpers ----------

const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
const mul = (a, k) => [a[0] * k, a[1] * k, a[2] * k];
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const clamp01 = (v) => Math.max(0, Math.min(1, v));
const vec = (o) => [o.x, o.y, o.z];
const finiteVec = (o) =>
  !!o && [o.x, o.y, o.z].every((n) => Number.isFinite(n));

// Closest distance between segment p1-q1 (the shot ray) and p2-q2 (player axis).
// Returns the distance and `s`, the position along the ray (0..1) of the closest point.
function closestSegSeg(p1, q1, p2, q2) {
  const d1 = sub(q1, p1);
  const d2 = sub(q2, p2);
  const r = sub(p1, p2);
  const a = dot(d1, d1);
  const e = dot(d2, d2);
  const f = dot(d2, r);
  const c = dot(d1, r);
  const b = dot(d1, d2);
  const denom = a * e - b * b;

  let s = denom > 1e-8 ? clamp01((b * f - c * e) / denom) : 0;
  let t = (b * s + f) / e;

  if (t < 0) {
    t = 0;
    s = clamp01(-c / a);
  } else if (t > 1) {
    t = 1;
    s = clamp01((b - c) / a);
  }

  const diff = sub(add(p1, mul(d1, s)), add(p2, mul(d2, t)));
  return { dist: Math.sqrt(dot(diff, diff)), s };
}

// Returns the closest living enemy hit by the ray, or null.
// NOTE: no world geometry on the server yet, so walls do not block shots.
function findHit(room, shooter, origin, dir, range) {
  const end = add(origin, mul(dir, range));
  let best = null;

  for (const target of room.members.values()) {
    if (target.id === shooter.id || !finiteVec(target.state)) continue;
    if (getCombatState(target).dead) continue;

    const c = vec(target.state);
    const { dist, s } = closestSegSeg(
      origin,
      end,
      [c[0], c[1] - PLAYER_HALF_SEGMENT, c[2]],
      [c[0], c[1] + PLAYER_HALF_SEGMENT, c[2]],
    );

    if (dist <= PLAYER_RADIUS && (!best || s < best.s)) {
      best = { target, s };
    }
  }

  return best ? best.target : null;
}

// ---------- combat state ----------

function getCombatState(session) {
  let state = players.get(session.id);

  if (!state) {
    state = {
      health: MAX_HEALTH,
      dead: false,
      weaponId: "rifle",
      ammo: WEAPONS.rifle.magazineSize,
      reserveAmmo: WEAPONS.rifle.reserveAmmo,
      lastShotAt: 0,
      respawnTimer: null,
    };
    players.set(session.id, state);
  }

  return state;
}

function publicState(session) {
  const p = getCombatState(session);

  return {
    playerId: session.id,
    health: p.health,
    dead: p.dead,
    weaponId: p.weaponId,
    ammo: p.ammo,
    reserveAmmo: p.reserveAmmo,
  };
}

function broadcast(room, message) {
  if (room) room.broadcast(message);
}

function syncPlayer(session, room) {
  broadcast(room, {
    t: "combat_state",
    data: publicState(session),
  });
}

function reject(session, reason, extra = {}) {
  session.send({
    t: "fire_result",
    data: { accepted: false, reason, ...extra },
  });
}

// ---------- handlers ----------

function handleFire(session, message) {
  const room = rooms.get(session.roomId);
  const p = getCombatState(session);
  const weapon = WEAPONS[p.weaponId];

  if (!room || room.roomState?.phase !== "playing") {
    return reject(session, "match_not_playing");
  }

  if (p.dead) return reject(session, "player_dead");
  if (!weapon) return reject(session, "invalid_weapon");
  if (p.ammo <= 0) return reject(session, "no_ammo");

  // Validate the ray. The client sends an aim ray, never a hit claim.
  const data = message.data || {};
  if (!finiteVec(data.direction) || !finiteVec(data.origin)) {
    return reject(session, "invalid_ray");
  }
  if (!finiteVec(session.state)) return reject(session, "no_state");

  const origin = vec(data.origin);
  const dir = vec(data.direction);
  const len = Math.sqrt(dot(dir, dir));
  if (len < 1e-6) return reject(session, "invalid_direction");
  const ndir = mul(dir, 1 / len);

  // Ray must start near the shooter's own position (anti-teleport-shot).
  const off = sub(origin, vec(session.state));
  if (Math.sqrt(dot(off, off)) > MAX_ORIGIN_OFFSET) {
    return reject(session, "bad_origin");
  }

  const now = Date.now();
  const cooldownMs = 1000 / weapon.fireRate;
  if (now - p.lastShotAt < cooldownMs * 0.75) {
    return reject(session, "fire_rate_exceeded");
  }

  p.lastShotAt = now;
  p.ammo--;

  const victim = findHit(room, session, origin, ndir, weapon.range);

  session.send({
    t: "fire_result",
    data: {
      accepted: true,
      shotId: data.shotId ?? null,
      ammo: p.ammo,
      reserveAmmo: p.reserveAmmo,
      hit: victim ? victim.id : null,
    },
  });

  broadcast(room, {
    t: "shot_fired",
    data: {
      playerId: session.id,
      shotId: data.shotId ?? null,
      weaponId: p.weaponId,
      origin: data.origin,
      direction: data.direction,
      hit: victim ? victim.id : null,
    },
  });

  if (victim) applyDamage(room, session.id, victim);
  else syncPlayer(session, room);
}

function handleReload(session) {
  const room = rooms.get(session.roomId);
  const p = getCombatState(session);
  const weapon = WEAPONS[p.weaponId];

  if (!room || room.roomState?.phase !== "playing") {
    return session.send({
      t: "reload_result",
      data: { accepted: false, reason: "match_not_playing" },
    });
  }

  if (
    p.dead ||
    !weapon ||
    p.reserveAmmo <= 0 ||
    p.ammo >= weapon.magazineSize
  ) {
    return session.send({
      t: "reload_result",
      data: { accepted: false, reason: "reload_not_needed" },
    });
  }

  const amount = Math.min(weapon.magazineSize - p.ammo, p.reserveAmmo);

  p.ammo += amount;
  p.reserveAmmo -= amount;

  session.send({
    t: "reload_result",
    data: {
      accepted: true,
      ammo: p.ammo,
      reserveAmmo: p.reserveAmmo,
    },
  });

  syncPlayer(session, room);
}

// Call only after the server has independently verified a hit.
// Never call this using an untrusted client-supplied damage value.
function applyDamage(room, attackerId, targetSession) {
  if (!room || !targetSession || targetSession.roomId !== room.id) {
    return false;
  }

  const target = getCombatState(targetSession);
  const attacker = room.members.get(attackerId);

  if (!attacker || target.dead) return false;

  const weapon = WEAPONS[getCombatState(attacker).weaponId];
  if (!weapon) return false;

  target.health = Math.max(0, target.health - weapon.damage);

  broadcast(room, {
    t: "health_update",
    data: {
      playerId: targetSession.id,
      health: target.health,
    },
  });

  if (target.health === 0) {
    target.dead = true;

    broadcast(room, {
      t: "player_died",
      data: {
        playerId: targetSession.id,
        killerId: attackerId,
      },
    });

    clearTimeout(target.respawnTimer);
    target.respawnTimer = setTimeout(() => {
      // Do not respawn someone who has left this room.
      if (targetSession.roomId !== room.id) return;

      target.health = MAX_HEALTH;
      target.dead = false;
      target.ammo = WEAPONS[target.weaponId].magazineSize;
      target.reserveAmmo = WEAPONS[target.weaponId].reserveAmmo;
      target.lastShotAt = 0;

      broadcast(room, {
        t: "respawn",
        data: publicState(targetSession),
      });
    }, RESPAWN_DELAY_MS);

    target.respawnTimer.unref?.();
  }

  return true;
}

function initializePlayer(session) {
  return publicState(session);
}

function cleanupPlayer(sessionId) {
  const p = players.get(sessionId);
  if (p) clearTimeout(p.respawnTimer);
  players.delete(sessionId);
}

module.exports = {
  getCombatState,
  publicState,
  initializePlayer,
  handleFire,
  handleReload,
  applyDamage,
  cleanupPlayer,
};
