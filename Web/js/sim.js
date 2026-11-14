'use strict';
// Simulacion: banda autonoma, guardias, camaras, controles de radio y policia oculta.
(function () {
  const E = window.EYE, C = E.CONST, W = C.MAP_W, H = C.MAP_H;

  // ---------- Rejilla fija ----------
  const roomGrid = new Array(W * H).fill(null);
  const doorGrid = new Array(W * H).fill(null);
  const blockGrid = new Uint8Array(W * H);
  const ROOM = (E.ROOM = {});

  E.ROOMS.forEach(r => {
    ROOM[r.id] = r;
    for (let y = r.y; y < r.y + r.h; y++) for (let x = r.x; x < r.x + r.w; x++) roomGrid[y * W + x] = r.id;
  });
  E.DOORS.forEach(d => { doorGrid[d.y * W + d.x] = d.id; });
  E.OBJECTS.forEach(o => {
    o.w = o.w || 1; o.d = o.d || 1; o.room = roomGrid[o.y * W + o.x];
    for (let y = o.y; y < o.y + o.d; y++) for (let x = o.x; x < o.x + o.w; x++) blockGrid[y * W + x] = 1;
  });
  E.ROOMS.forEach(r => {
    r.doorDefs = E.DOORS.filter(d => d.a === r.id || d.b === r.id);
    r.lamps = [];
    if (!r.exterior) {
      const n = Math.max(1, Math.round(Math.max(r.w, r.h) / 4));
      for (let i = 0; i < n; i++) {
        r.lamps.push(r.w >= r.h
          ? { x: r.x + ((i + 0.5) * r.w) / n, y: r.y + r.h / 2 }
          : { x: r.x + r.w / 2, y: r.y + ((i + 0.5) * r.h) / n });
      }
    }
  });

  const inside = (x, y) => x >= 0 && y >= 0 && x < W && y < H;
  E.roomAt = (x, y) => (inside(x, y) ? roomGrid[y * W + x] : null);
  E.doorIdAt = (x, y) => (inside(x, y) ? doorGrid[y * W + x] : null);

  let S = null;
  const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
  const cellOf = a => ({ x: Math.floor(a.x), y: Math.floor(a.y) });
  const doorConnects = (doorId, roomId) => { const d = S.doors[doorId].def; return d.a === roomId || d.b === roomId; };
  E.inRoomView = (a, roomId) => a.room === roomId || (a.onDoor && doorConnects(a.onDoor, roomId));

  // ---------- Caminos ----------
  const DIRS = [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]];
  function passable(x, y, opt) {
    if (!inside(x, y)) return false;
    const i = y * W + x, did = doorGrid[i];
    if (did) return opt.ignoreLocks || !S.doors[did].locked;
    if (!roomGrid[i]) return false;
    return !blockGrid[i];
  }
  function findPath(sx, sy, tx, ty, opt) {
    opt = opt || {};
    if (sx === tx && sy === ty) return [];
    const prev = new Int32Array(W * H).fill(-1), start = sy * W + sx, target = ty * W + tx;
    prev[start] = start;
    const q = [start];
    for (let qi = 0; qi < q.length; qi++) {
      const c = q[qi];
      if (c === target) break;
      const cx = c % W, cy = (c / W) | 0;
      for (let k = 0; k < 8; k++) {
        const nx = cx + DIRS[k][0], ny = cy + DIRS[k][1];
        if (!inside(nx, ny)) continue;
        const n = ny * W + nx;
        if (prev[n] !== -1 || !passable(nx, ny, opt)) continue;
        if (k >= 4) {
          if (doorGrid[c] || doorGrid[n] || doorGrid[cy * W + nx] || doorGrid[ny * W + cx]) continue;
          if (!passable(nx, cy, opt) || !passable(cx, ny, opt)) continue;
        }
        prev[n] = c;
        q.push(n);
      }
    }
    if (prev[target] === -1) return null;
    const path = [];
    for (let c = target; c !== start; c = prev[c]) path.push({ x: c % W, y: (c / W) | 0 });
    return path.reverse();
  }
  E.findPath = (a, b, c, d, o) => findPath(a, b, c, d, o);

  function locate(a) {
    const cx = Math.floor(a.x), cy = Math.floor(a.y);
    const r = roomGrid[cy * W + cx];
    if (r) a.room = r;
    a.onDoor = doorGrid[cy * W + cx] || null;
  }
  function stepMove(a, dt) {
    if (!a.path.length) { a.walking = false; return; }
    const n = a.path[0], tx = n.x + 0.5, ty = n.y + 0.5;
    const dx = tx - a.x, dy = ty - a.y, d = Math.hypot(dx, dy);
    const sp = a.speed * (a.crouch ? 0.7 : 1) * dt;
    if (d > 1e-4) { a.fx = dx / d; a.fy = dy / d; }
    if (d <= sp) { a.x = tx; a.y = ty; a.path.shift(); } else { a.x += (dx / d) * sp; a.y += (dy / d) * sp; }
    a.walking = true;
    a.anim += dt;
    locate(a);
  }
  function face(a, b) {
    const dx = b.x - a.x, dy = b.y - a.y, d = Math.hypot(dx, dy);
    if (d > 1e-3) { a.fx = dx / d; a.fy = dy / d; }
  }

  // ---------- Radio ----------
  function fill(s, v) { return s.replace(/\{(\w+)\}/g, (m, k) => (v[k] != null ? v[k] : m)); }
  let lastVariant = {};
  E.later = (delay, fn) => { if (delay <= 0) fn(); else S.timers.push({ at: S.t + delay, fn }); };
  E.say = function (key, vars, delay) {
    vars = vars || {};
    delay = delay || 0;
    let seq = E.LINES[key];
    if (!seq) {
      const v = E.VARIANTS[key];
      if (!v) return;
      let i = Math.floor(Math.random() * v.length);
      if (i === lastVariant[key]) i = (i + 1) % v.length;
      lastVariant[key] = i;
      seq = [v[i]];
    }
    seq.forEach((l, i) => E.later(delay + i * 1.9, () => E.radioLine(l[0], fill(l[1], vars), fill(l[2], vars))));
  };
  E.radioLine = function (ch, who, text) {
    const e = { t: S.t, ch, who, text };
    S.radio.push(e);
    if (E.ui && E.ui.onRadio) E.ui.onRadio(e);
    if (E.audio) E.audio.speak(who, text, ch);
  };
  E.toast = text => { if (E.ui && E.ui.toast) E.ui.toast(text); };

  // ---------- Peticiones de la banda ----------
  // Se refrescan cada tic mientras su condicion siga viva; si no, desaparecen solas.
  E.request = function (key, from, text, actions, opts) {
    let r = S.requests.find(q => q.key === key);
    if (!r) {
      r = { key, from, born: S.t };
      S.requests.push(r);
      if (E.audio) E.audio.ping();
    }
    Object.assign(r, { from, text, actions, seen: S.t, timer: null, urgent: false }, opts || {});
    return r;
  };
  function pruneRequests() { S.requests = S.requests.filter(r => S.t - r.seen < 0.4); }

  // ---------- Estado ----------
  function makeActor(name, kind, x, y, o) {
    const a = Object.assign({
      name, kind, x: x + 0.5, y: y + 0.5, fx: 1, fy: 0, path: [], speed: 1.7, walking: false, crouch: false,
      downed: false, anim: 0, detect: 0, state: 'normal', wait: 0, onArrive: null, afterWait: null, room: null, onDoor: null,
    }, o || {});
    locate(a);
    S.actors.push(a);
    return a;
  }

  E.newGame = function (tuneName) {
    E.TUNE = E.TUNES[tuneName] || E.TUNE;
    lastVariant = {};
    S = E.S = {
      t: 0, started: false, paused: false, over: null, endT: 0, view: 'alley', ir: false, autoFollow: true, manualT: -99,
      rooms: {}, doors: {}, actors: [], requests: [], timeline: [], radio: [], timers: [], fx: [], lock: {},
      hack: null, phoneCd: 0, ringRoom: null, ringT: 0, staticT: 0, flags: {},
      stats: { hacksOk: 0, hacksFail: 0, attemptsLost: 0, takedowns: 0 },
      police: { susp: 0, state: 0, eta: 0, unit: 'UNIDAD 7', ms: {}, chatterT: 22 },
      checkins: { next: 60, pending: null },
      team: { step: 0, startT: 4, busy: null, busyT: 0, code: false, loot: false, hunt: null, huntRush: false, huntT: 0, huntRepath: 0,
        cleared: null, repath: true, said: {}, camWait: {}, guardWait: 0, trail: [], holdNow: null, lastRoom: 'alley' },
      lastCause: null, betrayedBy: null,
    };
    E.ROOMS.forEach(r => { S.rooms[r.id] = { id: r.id, def: r, lit: true, loopT: 0, camDetect: 0, camCd: 0, cutOnce: false }; });
    E.DOORS.forEach(d => { S.doors[d.id] = { id: d.id, def: d, locked: !!d.locked, openT: 0 }; });
    S.vito = makeActor('VITO', 'crew', 2, 17, { fx: 1, fy: -1 });
    S.nora = makeActor('NORA', 'crew', 1, 17, { fx: 1, fy: -1 });
    S.crew = [S.vito, S.nora];
    S.ramirez = makeActor('RAMÍREZ', 'guard', 16, 10, { speed: 1.15, fx: -1, fy: 0, brain: ramirezBrain, wp: 0 });
    S.castro = makeActor('CASTRO', 'guard', 20, 13, { speed: 1.1, fx: 0, fy: -1, brain: castroBrain, atPost: true });
    S.guards = [S.ramirez, S.castro];
    S.team.trail = [{ x: 1, y: 17 }, { x: 2, y: 17 }];
    return S;
  };

  E.start = function () {
    S.started = true;
    E.say('crew_start', {}, 0.6);
  };

  E.setView = function (id, byPlayer) {
    if (!ROOM[id]) return;
    if (S.view !== id) { S.staticT = 0.18; if (E.audio) E.audio.click(); }
    S.view = id;
    if (byPlayer) S.manualT = S.t;
  };

  // ---------- Suspicacia y policia ----------
  function mark(text) { S.timeline.push({ t: S.t, text, important: true }); }
  E.raise = function (amount, text) {
    amount *= E.TUNE.suspicion;
    S.police.susp += amount;
    S.lastCause = text;
    S.timeline.push({ t: S.t, text, add: amount });
    policeThresholds();
  };
  function policeThresholds() {
    const P = S.police;
    if (S.over) return;
    // Solo se anuncia el escalon mas alto alcanzado, para no mezclar mensajes.
    const target = P.susp >= 80 ? 3 : P.susp >= 50 ? 2 : P.susp >= 25 ? 1 : 0;
    if (target <= P.state) return;
    if (target >= 2 && !S.betrayedBy) S.betrayedBy = S.lastCause;
    if (target === 1) { E.say('pol_rumor', {}, 1.5); mark('La central oye algo raro'); }
    if (target === 2) { P.eta = 110; P.unit = 'UNIDAD 7'; P.ms = {}; E.say('pol_verify', {}, 2); mark('Sale la Unidad 7, sin prisa'); }
    if (target === 3) {
      P.eta = P.state === 2 ? Math.min(P.eta, 50) : 50;
      P.unit = 'UNIDAD 14'; P.ms = {};
      E.say('pol_enroute', {}, 2); mark('Sale la Unidad 14, con prioridad');
    }
    P.state = target;
  }
  function policeUpdate(dt) {
    const P = S.police;
    if (P.state < 2) P.susp = Math.max(0, P.susp - 0.1 * dt);
    if (P.state === 1 && P.susp < 12) P.state = 0;
    if (P.state >= 2) {
      P.eta -= dt;
      if (P.state === 2) {
        if (!P.ms.a && P.eta < 70) { P.ms.a = 1; E.say('pol_eta_far'); }
        if (!P.ms.b && P.eta < 28) { P.ms.b = 1; E.say('pol_eta_near'); E.say('crew_police_close', {}, 3); }
      } else if (!P.ms.a && P.eta < 25) { P.ms.a = 1; E.say('pol_eta14'); }
      if (P.eta <= 0) lose();
    }
    P.chatterT -= dt;
    if (P.chatterT <= 0) { P.chatterT = 30 + Math.random() * 20; if (P.state < 3) E.say('pol_chatter'); }
    if (!S.flags.roundNotice && S.t > 12) { S.flags.roundNotice = true; E.say('pol_round_notice'); }
    if (!S.flags.round && S.t >= C.ROUND_AT) {
      S.flags.round = true;
      const dark = Object.values(S.rooms).some(r => !r.lit);
      E.say(dark ? 'pol_round_odd' : 'pol_round_pass');
      E.raise(dark ? 30 : 12, dark ? 'La ronda de las 02:30 vio el edificio a oscuras' : 'La ronda de las 02:30 pasó por delante');
    }
  }
  function lose() {
    if (S.over) return;
    S.over = 'lose'; S.endT = 0;
    if (S.hack) E.hack.cancel();
    E.say('pol_arrived', { unit: S.police.unit });
  }
  function win() {
    if (S.over) return;
    S.over = 'win'; S.endT = 0;
    if (S.hack) E.hack.cancel();
    E.say('crew_escape');
  }

  // ---------- Guardias ----------
  const PATROL = [[21, 10], [9, 10], [12, 14], [14, 17], [12, 12]];
  const POST = [20, 13];
  function goTo(a, x, y, onArrive) {
    const c = cellOf(a);
    a.path = findPath(c.x, c.y, x, y, {}) || [];
    a.onArrive = onArrive || null;
    if (a.kind === 'guard') a.atPost = false;
  }
  function interrupt(g, x, y, onArrive) { g.wait = 0; g.afterWait = null; goTo(g, x, y, onArrive); }
  function ramirezBrain(g) {
    const p = PATROL[g.wp % PATROL.length];
    g.wp++;
    goTo(g, p[0], p[1], () => { g.wait = 2.2; });
  }
  function castroBrain(g) {
    if (g.route && g.route.length) {
      const p = g.route.shift();
      goTo(g, p[0], p[1], () => {
        g.wait = 1.6;
        if (!g.route.length) g.afterWait = () => E.say('sec_round_back');
      });
      return;
    }
    const c = cellOf(g);
    if (c.x === POST[0] && c.y === POST[1]) { g.atPost = true; g.fx = 0; g.fy = -1; g.walking = false; return; }
    goTo(g, POST[0], POST[1]);
  }
  function runTask(g, dt) {
    if (g.wait > 0) {
      g.wait -= dt;
      g.walking = false;
      if (g.wait <= 0 && g.afterWait) { const f = g.afterWait; g.afterWait = null; f(g); }
      return;
    }
    if (g.path.length) {
      stepMove(g, dt);
      if (!g.path.length && g.onArrive) { const f = g.onArrive; g.onArrive = null; f(g); }
      return;
    }
    if (g.onArrive) { const f = g.onArrive; g.onArrive = null; f(g); return; }
    g.walking = false;
    g.brain(g);
  }
  function castroWatching() {
    const c = S.castro;
    return !c.downed && c.atPost && c.state === 'normal';
  }
  E.castroWatching = () => castroWatching();

  // Cuanto ve un guardia a un miembro de la banda (0 = nada).
  function sight(g, c) {
    if (!(c.room === g.room || (c.onDoor && doorConnects(c.onDoor, g.room)))) return 0;
    const lit = S.rooms[c.room].lit;
    const dx = c.x - g.x, dy = c.y - g.y, d = Math.hypot(dx, dy);
    const range = lit ? 6 : 3.2;
    if (d > range) return 0;
    if (d < 0.9) return 2;
    const dot = (dx * g.fx + dy * g.fy) / d;
    if (dot < (lit ? 0.5 : 0.85)) return 0;
    return 1 - (0.5 * d) / range;
  }
  function startCall(g, who) {
    g.state = 'calling'; g.callT = 2.6; g.callTarget = who; g.path = []; g.onArrive = null; g.wait = 0;
    E.say('crew_spotted');
    if (E.audio) E.audio.alert();
    startHunt(g, true);
  }
  function finishCall(g) {
    g.state = 'alarmed';
    E.say('sec_intruders', { who: g.name, in: ROOM[g.room].inP });
    E.raise(35, `${g.name} vio a la banda y dio el aviso`);
    E.say('crew_call_done', {}, 2.2);
  }
  function guardUpdate(g, dt) {
    if (g.downed) return;
    if (g.state === 'calling') {
      g.callT -= dt; g.walking = false; face(g, g.callTarget);
      if (g.callT <= 0) finishCall(g);
      return;
    }
    if (g.state === 'alarmed') { g.walking = false; face(g, S.vito); return; }
    let best = 0, who = null;
    for (const c of S.crew) {
      const f = sight(g, c);
      if (f <= 0) continue;
      const rate = (f * (c.crouch ? 0.5 : 1)) / (S.rooms[c.room].lit ? 1.5 : 2.3);
      if (rate > best) { best = rate; who = c; }
    }
    if (best > 0) {
      g.detect += best * dt;
      g.seen = who;
      if (g.detect >= 1) { startCall(g, who); return; }
    } else g.detect = Math.max(0, g.detect - 0.3 * dt);
    if (g.detect > 0.35 && g.seen) { face(g, g.seen); g.walking = false; return; }
    runTask(g, dt);
  }
  function takedown(g) {
    const wasCalling = g.state === 'calling';
    Object.assign(g, { downed: true, state: 'down', path: [], walking: false, atPost: false, wait: 0, onArrive: null, afterWait: null, detect: 0 });
    S.stats.takedowns++;
    // Un guardia que desaparece no tarda en echarse de menos: control por radio pronto.
    S.checkins.next = Math.min(S.checkins.next, S.t + 22 + Math.random() * 8);
    S.fx.push({ kind: 'hit', x: g.x, y: g.y, room: g.room, t: 0.5 });
    if (E.audio) E.audio.thud();
    if (wasCalling) { E.say('crew_cut_call'); E.raise(8, `La radio de ${g.name} se cortó a media frase`); } else E.say('crew_takedown');
  }

  // ---------- Camaras ----------
  function camUpdate(dt) {
    for (const id in S.rooms) {
      const rs = S.rooms[id], R = rs.def;
      rs.loopT = Math.max(0, rs.loopT - dt);
      rs.camCd = Math.max(0, rs.camCd - dt);
      if (!R.watched || rs.loopT > 0 || !rs.lit || !castroWatching() || rs.camCd > 0) { rs.camDetect = Math.max(0, rs.camDetect - dt * 0.5); continue; }
      if (S.crew.some(c => c.room === id)) {
        rs.camDetect += dt / 2.4;
        if (rs.camDetect >= 1) { rs.camDetect = 0; rs.camCd = 25; cameraSpotted(R); }
      } else rs.camDetect = Math.max(0, rs.camDetect - dt * 0.5);
    }
  }
  function freeCellIn(R, nearX, nearY) {
    let best = null, bd = 1e9;
    for (let y = R.y; y < R.y + R.h; y++) for (let x = R.x; x < R.x + R.w; x++) {
      if (blockGrid[y * W + x]) continue;
      const d = Math.hypot(x - nearX, y - nearY);
      if (d < bd) { bd = d; best = { x, y }; }
    }
    return best;
  }
  function cameraSpotted(R) {
    E.raise(18, `Castro vio a la banda en la cámara ${R.ofP}`);
    E.say('sec_cam', { of: R.ofP });
    const c = S.castro;
    const spot = freeCellIn(R, R.x + R.w / 2, R.y + R.h / 2);
    if (spot) interrupt(c, spot.x, spot.y, g => { g.wait = 5; g.afterWait = () => E.say('sec_nothing'); });
  }

  // ---------- Acciones del jugador sin hackeo ----------
  E.toggleLights = function (roomId) {
    const R = ROOM[roomId];
    if (!R || R.exterior || S.over) return;
    const rs = S.rooms[roomId];
    rs.lit = !rs.lit;
    if (E.audio) E.audio.clunk();
    if (rs.lit) { E.toast(`Luz ${R.inP}: encendida`); return; }
    E.toast(`Luz ${R.inP}: apagada`);
    if (!rs.cutOnce) { rs.cutOnce = true; E.raise(3, `Corte de luz ${R.inP}`); }
    if (!S.flags.outage) { S.flags.outage = true; E.say('pol_outage', {}, 7); }
    if (!S.castro.downed && S.castro.state === 'normal') E.later(2, () => { if (!S.castro.downed) E.say('sec_lights', { in: R.inP }); });
    if (S.crew.some(c => c.room === roomId) && !S.flags.darkLine) { S.flags.darkLine = true; E.say('crew_dark', {}, 1); }
  };

  E.callPhone = function (roomId) {
    const R = ROOM[roomId];
    if (!R || !R.phone || S.over) return;
    if (S.phoneCd > 0) { E.toast(`Teléfono en espera: ${Math.ceil(S.phoneCd)} s`); return; }
    S.phoneCd = 12; S.ringRoom = roomId; S.ringT = 3.5;
    if (E.audio) E.audio.ring();
    let best = null, bl = 1e9;
    for (const g of S.guards) {
      if (g.downed || g.state !== 'normal') continue;
      const c = cellOf(g);
      const p = findPath(c.x, c.y, R.phone.x, R.phone.y, {});
      if (p && p.length < bl) { bl = p.length; best = g; }
    }
    if (!best || bl > 40) { E.toast('Suena el teléfono... y nadie lo oye.'); return; }
    E.toast(`${best.name} va a contestar ${R.inP}`);
    interrupt(best, R.phone.x, R.phone.y, g => {
      g.wait = 4.5;
      S.ringT = 0;
      E.say('sec_phone', { who: g.name });
    });
  };

  // ---------- Controles por radio ----------
  function checkinUpdate(dt) {
    const K = S.checkins;
    if (K.pending) {
      const spoofing = S.hack && S.hack.spec.key === 'spoof';
      if (!spoofing) K.pending.t -= dt;
      E.request('spoof', 'NORA',
        K.pending.by === 'castro' ? 'Castro llama a Ramírez por radio. Contesta tú con su voz.' : 'La central llama a Castro. Contesta tú con su voz.',
        [{ label: 'SUPLANTAR VOZ', act: 'spoof' }], { timer: Math.max(0, K.pending.t / K.pending.max), urgent: true });
      if (K.pending.t <= 0) missedCheck(false);
      return;
    }
    if (S.t < K.next) return;
    K.next = S.t + 80;
    const castro = S.castro, ram = S.ramirez;
    if (!castro.downed && castro.state === 'normal') {
      E.say('sec_checkin');
      if (!ram.downed && ram.state === 'normal') E.later(2.4, () => E.say('sec_checkin_ok'));
      else E.later(3.5, () => { if (S.over) return; E.say('sec_checkin_none'); K.pending = { t: 16, max: 16, by: 'castro', as: 'RAMÍREZ' }; E.say('crew_spoof', {}, 1.5); });
    } else if (castro.downed) {
      E.say('sec_central_check');
      E.later(1.5, () => { if (S.over) return; K.pending = { t: 16, max: 16, by: 'central', as: 'CASTRO' }; E.say('crew_spoof', {}, 1); });
    }
  }
  function spoofOk() {
    const p = S.checkins.pending;
    S.checkins.pending = null;
    if (!p) return;
    E.say(p.by === 'castro' ? 'sec_spoof_ok' : 'sec_central_spoof');
  }
  function missedCheck(badVoice) {
    const p = S.checkins.pending;
    S.checkins.pending = null;
    if (!p) return;
    if (p.by === 'castro') {
      E.say(badVoice ? 'sec_spoof_bad' : 'sec_checkin_lost');
      E.raise(25, 'Ramírez no contestó al control de radio');
      const c = S.castro, r = S.ramirez;
      if (!c.downed && c.state === 'normal') {
        const rc = cellOf(r);
        interrupt(c, rc.x, rc.y, g => {
          g.wait = 4;
          if (r.downed && !S.flags.bodyFound) { S.flags.bodyFound = true; E.say('sec_body'); E.raise(20, 'Castro encontró a Ramírez en el suelo'); }
        });
      }
    } else {
      E.say('sec_central_none');
      E.raise(35, 'Castro no contestó al control de la central');
    }
  }

  // ---------- Banda autonoma ----------
  const STEPS = [
    { kind: 'terminal', goal: [11, 3] },
    { kind: 'loot', goal: [19, 5] },
    { kind: 'exit', goal: [1, 17] },
  ];

  function distractActions(g) {
    const acts = [];
    const phoneRoom = ROOM[g.room].phone ? g.room : (S.vito.room !== 'office' ? 'office' : 'dock');
    acts.push({ label: 'TELÉFONO ' + ROOM[phoneRoom].name, act: 'phone', arg: phoneRoom });
    if (!ROOM[g.room].exterior) acts.push({ label: S.rooms[g.room].lit ? 'APAGAR LUZ' : 'LUZ YA APAGADA', act: 'lights', arg: g.room, off: !S.rooms[g.room].lit });
    return acts;
  }
  function loopActions(roomId) {
    return [{ label: 'BUCLE DE CÁMARA', act: 'loop', arg: roomId }, { label: 'APAGAR LUZ', act: 'lights', arg: roomId }];
  }
  function startHunt(g, rush) {
    const T = S.team;
    if (T.hunt === g) { T.huntRush = T.huntRush || rush; return; }
    T.hunt = g; T.huntRush = rush; T.huntT = 0; T.huntRepath = 0;
    if (!rush) E.say('crew_hunt');
  }
  function hunt(dt) {
    const T = S.team, L = S.vito, g = T.hunt;
    const stop = () => { T.hunt = null; T.repath = true; L.speed = 1.7; L.path = []; };
    if (g.downed) { stop(); return; }
    T.huntT += dt;
    if (T.huntT > 14) { stop(); return; }
    const rush = T.huntRush || g.state !== 'normal';
    L.speed = rush ? 2.9 : 2.0;
    L.crouch = !rush;
    T.huntRepath -= dt;
    if (T.huntRepath <= 0) {
      T.huntRepath = 0.35;
      const a = cellOf(L), b = cellOf(g);
      const p = findPath(a.x, a.y, b.x, b.y, {});
      if (!p) { stop(); return; }
      L.path = p;
    }
    if (dist(L, g) < 0.95) { takedown(g); stop(); return; }
    stepMove(L, dt);
    trail(L);
  }
  function trail(L) {
    const c = cellOf(L), tr = S.team.trail, last = tr[tr.length - 1];
    if (!last || last.x !== c.x || last.y !== c.y) { tr.push(c); if (tr.length > 8) tr.shift(); }
  }
  function followerUpdate(dt, hold) {
    const F = S.nora, L = S.vito;
    F.crouch = L.crouch || !!hold;
    if (hold) { F.walking = false; return; }
    const tr = S.team.trail;
    const tgt = tr.length >= 2 ? tr[tr.length - 2] : null;
    if (tgt && (!F.goal || F.goal.x !== tgt.x || F.goal.y !== tgt.y)) {
      F.goal = tgt;
      const fc = cellOf(F);
      F.path = fc.x === tgt.x && fc.y === tgt.y ? [] : findPath(fc.x, fc.y, tgt.x, tgt.y, {}) || [];
    }
    if (F.path.length) { F.speed = dist(F, L) > 3 ? 2.3 : 1.7; stepMove(F, dt); } else { F.walking = false; if (dist(F, L) > 0.3) face(F, L); }
  }
  function sneaking(L) { return S.guards.some(g => !g.downed && g.room === L.room); }

  // Amenaza dentro de la sala de la banda. Devuelve true si la banda se queda quieta.
  function checkThreats(dt) {
    const T = S.team, L = S.vito;
    let g = null, bd = 1e9;
    for (const x of S.guards) {
      if (x.downed || !E.inRoomView(x, L.room)) continue;
      const d = dist(x, L);
      if (x.state !== 'normal') { if (d < 14) { startHunt(x, true); return true; } continue; }
      if (d < bd) { bd = d; g = x; }
    }
    if (!g || bd > 7.5) { delete T.said.near; return false; }
    if (!S.rooms[L.room].lit) { startHunt(g, false); return true; }
    if (bd < 2.8 || (g.detect > 0.4 && bd < 5.5)) { startHunt(g, true); return true; }
    L.crouch = true; L.walking = false;
    if (!T.said.near) { T.said.near = 1; E.say('crew_guard_near'); }
    E.request('guard:' + g.name, 'NORA', `${g.name} anda cerca ${ROOM[L.room].inP}. Distráelo o apaga la luz y Vito se encarga.`, distractActions(g), { urgent: true });
    return true;
  }
  function exposureRequests() {
    const L = S.vito, R = ROOM[L.room], rs = S.rooms[L.room];
    if (R.watched && rs.lit && castroWatching() && rs.loopT < 10) {
      E.request('loop:' + R.id, 'NORA', rs.loopT > 0 ? `El bucle ${R.ofP} se está acabando.` : `La cámara ${R.ofP} nos está grabando y Castro la mira.`, loopActions(R.id), { urgent: rs.loopT <= 0 });
      if (rs.camDetect > 0.25 && !S.team.said['camseen:' + R.id]) { S.team.said['camseen:' + R.id] = 1; E.say('crew_cam_seen', { in: R.ofP }); }
    }
  }
  function lookahead() {
    const L = S.vito, n = Math.min(L.path.length, 12);
    for (let i = 1; i < n; i++) {
      const c = L.path[i], did = doorGrid[c.y * W + c.x];
      if (!did) continue;
      const d = S.doors[did];
      if (!d.locked || d.def.vault) continue;
      E.request('door:' + d.id, 'NORA', `${d.def.name}: cerrada. Vamos hacia allí, ve abriéndola.`, [{ label: 'ABRIR PUERTA', act: 'door', arg: d.id }]);
      const k = 'ahead:' + d.id;
      if (!S.team.said[k] && i > 3) { S.team.said[k] = 1; E.say('crew_need_door_ahead', { door: d.def.name.toLowerCase().replace('puerta ', 'puerta ') }); }
    }
  }
  function nextDoor(L) {
    const n = L.path[0];
    if (!n) return null;
    const did = doorGrid[n.y * W + n.x];
    if (!did) return null;
    const d = S.doors[did];
    return { door: d, room: d.def.a === L.room ? d.def.b : d.def.a };
  }
  // Antes de cruzar una puerta: cerradura, guardia al otro lado y camara vigilada.
  function gate(door, roomId, dt) {
    const T = S.team, R = ROOM[roomId], rs = S.rooms[roomId];
    if (door.locked) {
      if (door.def.vault) {
        if (!T.said.vault) { T.said.vault = 1; E.say('crew_vault'); }
        E.request('vault', 'VITO', 'La banda está delante de la bóveda. Ábrela: huella y cortafuegos.', [{ label: 'ABRIR BÓVEDA', act: 'vault' }], { urgent: true });
      } else {
        const k = 'askdoor:' + door.id;
        if (!T.said[k]) { T.said[k] = 1; E.say('crew_need_door', { door: door.def.name }); }
        E.request('door:' + door.id, 'VITO', `${door.def.name}: cerrada. La banda espera delante.`, [{ label: 'ABRIR PUERTA', act: 'door', arg: door.id }], { urgent: true });
      }
      T.holdNow = 'door';
      return false;
    }
    const g = S.guards.find(x => !x.downed && x.room === roomId && Math.hypot(x.x - (door.def.x + 0.5), x.y - (door.def.y + 0.5)) < 9);
    if (g) {
      if (!rs.lit) { startHunt(g, false); return false; }
      const k = 'guardahead:' + roomId;
      if (!T.said[k]) { T.said[k] = 1; E.say('crew_guard_ahead', { in: R.inP }); }
      T.guardWait += dt;
      if (T.guardWait > 6) {
        if (!T.said[k + '2']) { T.said[k + '2'] = 1; E.say('crew_guard_stays'); }
        E.request('guard:' + g.name, 'VITO', `${g.name} está ${R.inP} y nos corta el paso.`, distractActions(g));
      }
      T.holdNow = 'guard';
      return false;
    }
    T.guardWait = 0;
    if (R.watched && rs.lit && rs.loopT < 12 && castroWatching()) {
      T.camWait[roomId] = (T.camWait[roomId] || 0) + dt;
      const k = 'askloop:' + roomId;
      if (!T.said[k]) { T.said[k] = 1; E.say('crew_need_loop', { in: R.inP }); }
      if (T.camWait[roomId] < 25) {
        E.request('loop:' + roomId, 'NORA', `Cámara ${R.ofP} vigilada por Castro. Esperan un poco; si no, entran igual.`, loopActions(roomId), { urgent: true });
        T.holdNow = 'cam';
        return false;
      }
      if (!T.said[k + 'go']) { T.said[k + 'go'] = 1; E.say('crew_go_anyway'); }
    }
    return true;
  }
  function clearGateMemory(roomId) {
    const T = S.team;
    ['askloop:', 'guardahead:'].forEach(p => { delete T.said[p + roomId]; delete T.said[p + roomId + '2']; delete T.said[p + roomId + 'go']; });
    T.camWait[roomId] = 0;
  }
  function arrive(st) {
    const T = S.team;
    if (st.kind === 'terminal') { if (T.busy !== 'terminal') { T.busy = 'terminal'; E.say('crew_terminal'); } }
    else if (st.kind === 'loot') { T.busy = 'loot'; T.busyT = 7; }
    else if (st.kind === 'exit' && S.crew.every(c => c.room === 'alley' && !c.onDoor)) win();
  }
  function busy(dt) {
    const T = S.team;
    if (T.busy === 'terminal') {
      if (T.code) { T.busy = null; T.step++; T.repath = true; E.say('crew_code'); return; }
      E.request('terminal', 'NORA', 'Nora está conectada al terminal. Saca el código de la bóveda.', [{ label: 'HACKEAR TERMINAL', act: 'terminal' }]);
    } else if (T.busy === 'loot') {
      T.busyT -= dt;
      S.vito.walking = false;
      if (T.busyT <= 0) { T.busy = null; T.loot = true; T.step++; T.repath = true; S.crew.forEach(c => { c.bag = true; }); E.say('crew_loot'); }
    }
  }
  function teamUpdate(dt) {
    const T = S.team, L = S.vito;
    T.holdNow = null;
    if (T.startT > 0) { T.startT -= dt; return; }
    if (T.hunt) { hunt(dt); followerUpdate(dt, true); return; }
    if (checkThreats(dt)) { followerUpdate(dt, true); return; }
    exposureRequests();
    if (T.busy) { busy(dt); followerUpdate(dt); return; }
    const st = STEPS[T.step];
    if (!st) return;
    const lc = cellOf(L);
    if (lc.x === st.goal[0] && lc.y === st.goal[1]) { L.path = []; L.walking = false; arrive(st); followerUpdate(dt); return; }
    if (!L.path.length || T.repath) { T.repath = false; L.path = findPath(lc.x, lc.y, st.goal[0], st.goal[1], { ignoreLocks: true }) || []; }
    lookahead();
    const tr = nextDoor(L);
    if (tr) {
      if (T.cleared !== tr.door.id) {
        if (!gate(tr.door, tr.room, dt)) { L.walking = false; L.crouch = sneaking(L); followerUpdate(dt); return; }
        T.cleared = tr.door.id;
        T.guardWait = 0;
        clearGateMemory(tr.room);
      }
    } else if (!L.onDoor) T.cleared = null;
    L.crouch = sneaking(L);
    stepMove(L, dt);
    trail(L);
    followerUpdate(dt);
  }

  // ---------- Acciones desde la interfaz ----------
  E.act = function (act, arg) {
    if (!S || !S.started || S.over) return;
    const T = S.team;
    switch (act) {
      case 'view': E.setView(arg, true); break;
      case 'lights': E.toggleLights(arg); E.setView(arg, true); break;
      case 'phone': E.callPhone(arg); break;
      case 'door': {
        const d = S.doors[arg];
        if (!d || !d.locked || d.def.vault) return;
        E.setView(E.inRoomView(S.vito, d.def.a) ? d.def.a : E.inRoomView(S.vito, d.def.b) ? d.def.b : d.def.a, true);
        E.hack.start({
          key: 'door:' + arg, title: d.def.name.toUpperCase(), stages: ['sync'],
          onWin: () => {
            d.locked = false;
            E.toast(`${d.def.name}: abierta`);
            if (Math.hypot(S.vito.x - d.def.x - 0.5, S.vito.y - d.def.y - 0.5) < 3) E.say('crew_door_open');
          },
        });
        break;
      }
      case 'loop': {
        const rs = S.rooms[arg];
        if (!rs || rs.loopT > 30) return;
        E.setView(arg, true);
        E.hack.start({
          key: 'loop:' + arg, title: 'BUCLE · CAM ' + ROOM[arg].cam + ' ' + ROOM[arg].name, stages: ['loop'],
          onWin: () => { rs.loopT = 120; rs.camDetect = 0; E.toast(`Cámara ${ROOM[arg].ofP} en bucle durante 2 minutos`); },
        });
        break;
      }
      case 'terminal':
        if (T.busy !== 'terminal' || T.code) return;
        E.setView('servers', true);
        E.hack.start({ key: 'terminal', title: 'TERMINAL · CÓDIGO DE LA BÓVEDA', stages: ['crypt'], onWin: () => { T.code = true; } });
        break;
      case 'vault':
        if (!T.code || !S.doors.vault.locked) return;
        E.setView('hall', true);
        E.hack.start({
          key: 'vault', title: 'BÓVEDA · HUELLA Y CORTAFUEGOS', stages: ['print', 'firewall'],
          onWin: () => { S.doors.vault.locked = false; E.say('crew_vault_open'); },
        });
        break;
      case 'spoof':
        if (!S.checkins.pending) return;
        E.hack.start({ key: 'spoof', title: 'SUPLANTAR VOZ · ' + S.checkins.pending.as, stages: ['patch'], onWin: spoofOk, onFail: () => missedCheck(true) });
        break;
    }
  };

  // ---------- Bucle principal de simulacion ----------
  function schedule() {
    const c = S.castro;
    if (!S.flags.castroRound && S.t >= 150) {
      if (c.downed) { S.flags.castroRound = true; return; }
      if (c.state === 'normal' && c.atPost) {
        S.flags.castroRound = true;
        c.route = [[18, 10], [21, 10], [9, 10], [4, 12], [9, 10]];
        c.atPost = false;
        E.say('sec_round');
      }
    }
  }
  function autoFollow() {
    const L = S.vito;
    // Cambio de sala pendiente: se aplica en cuanto el jugador deja de tocar las camaras unos segundos.
    if (L.room !== S.team.lastRoom && S.autoFollow && !S.hack && S.t - S.manualT > 3) {
      S.team.lastRoom = L.room;
      E.setView(L.room);
    }
  }

  E.sim = {
    update(dt) {
      if (!S.started || S.paused) return;
      S.t += dt;
      for (let i = S.timers.length - 1; i >= 0; i--) {
        if (S.timers[i] && S.timers[i].at <= S.t) { const f = S.timers[i].fn; S.timers.splice(i, 1); f(); }
      }
      if (S.over) { S.endT += dt; return; }
      S.phoneCd = Math.max(0, S.phoneCd - dt);
      S.ringT = Math.max(0, S.ringT - dt);
      teamUpdate(dt);
      for (const g of S.guards) guardUpdate(g, dt);
      camUpdate(dt);
      checkinUpdate(dt);
      schedule();
      policeUpdate(dt);
      for (const id in S.doors) {
        const d = S.doors[id];
        d.openT = Math.max(0, d.openT - dt);
        if (!d.locked && S.actors.some(a => !a.downed && Math.hypot(a.x - d.def.x - 0.5, a.y - d.def.y - 0.5) < 1.1)) d.openT = 0.4;
      }
      S.fx = S.fx.filter(f => (f.t -= dt) > 0);
      pruneRequests();
      autoFollow();
    },
  };

  E.debug = {
    state: () => S,
    solve: () => { if (S.hack) E.hack.solve(); },
    skip(sec) { for (let i = 0; i < sec * 20; i++) E.sim.update(0.05); },
  };
})();
