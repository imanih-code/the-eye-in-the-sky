'use strict';
// Interfaz de la consola: camaras, peticiones, acciones por sala, plano, radio y pantallas.
(function () {
  const E = window.EYE, C = E.CONST;
  const $ = s => document.querySelector(s);
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const pad = n => String(n).padStart(2, '0');
  const ui = (E.ui = {});
  E.input = { down: {} };
  let S, tune = 'normal';

  function clock(t) {
    const s = Math.floor(C.CLOCK_START + t * C.CLOCK_RATE);
    return `${pad(Math.floor(s / 3600) % 24)}:${pad(Math.floor(s / 60) % 60)}:${pad(s % 60)}`;
  }
  const TAG = { crew: 'BANDA', security: 'SEGURIDAD', police: 'ESCÁNER' };

  // ---------- Radio y avisos ----------
  ui.onRadio = e => {
    const log = $('#log');
    const el = document.createElement('div');
    el.className = 'ln new ch-' + e.ch;
    el.innerHTML = `<span class="ts">${clock(e.t).slice(0, 5)}</span><span class="tag">${TAG[e.ch]}</span><b>${esc(e.who)}:</b> ${esc(e.text)}`;
    log.appendChild(el);
    while (log.children.length > 80) log.removeChild(log.firstChild);
    log.scrollTop = log.scrollHeight;
    if (!E.audio.voice) E.audio.squelch(e.ch);
  };
  let toastT = 0;
  ui.toast = text => { const t = $('#toast'); t.textContent = text; t.classList.add('on'); toastT = 2.6; };

  // ---------- Botones ----------
  function lockLeft(key) { const l = S.lock[key]; return l && l > S.t ? Math.ceil(l - S.t) : 0; }
  function actionState(a) {
    if (a.off) return { dis: true, label: a.label };
    if (S.hack) return { dis: true, label: a.label };
    const keyMap = { door: 'door:' + a.arg, loop: 'loop:' + a.arg, terminal: 'terminal', vault: 'vault', spoof: 'spoof' };
    const k = keyMap[a.act];
    const lk = k ? lockLeft(k) : 0;
    if (lk) return { dis: true, label: `BLOQUEADO ${lk}s` };
    if (a.act === 'phone' && S.phoneCd > 0) return { dis: true, label: `TELÉFONO ${Math.ceil(S.phoneCd)}s` };
    if (a.act === 'loop' && S.rooms[a.arg].loopT > 30) return { dis: true, label: `EN BUCLE ${Math.ceil(S.rooms[a.arg].loopT)}s` };
    if (a.act === 'lights' && !S.rooms[a.arg].lit && a.label === 'APAGAR LUZ') return { dis: true, label: 'LUZ APAGADA' };
    return { dis: false, label: a.label };
  }
  const btn = (a, cls) => { const st = actionState(a); return `<button class="${cls || ''}" data-act="${a.act}" data-arg="${a.arg || ''}" ${st.dis ? 'disabled' : ''}>${esc(st.label)}${a.sub ? `<small>${esc(a.sub)}</small>` : ''}</button>`; };

  // ---------- Peticiones ----------
  let reqSig = '';
  function renderRequests() {
    const box = $('#reqs');
    const html = S.requests.length
      ? S.requests.slice().sort((a, b) => (b.urgent ? 1 : 0) - (a.urgent ? 1 : 0) || a.born - b.born).map(r =>
        `<div class="req ${r.urgent ? 'urgent' : ''}" data-key="${esc(r.key)}"><div class="rfrom">${esc(r.from)} PIDE</div><div class="rtext">${esc(r.text)}</div>${r.timer != null ? '<div class="rbar"><i></i></div>' : ''}<div class="racts">${r.actions.map(a => btn(a)).join('')}</div></div>`).join('')
      : `<div class="empty">${S.started ? 'Nada que pedir ahora. La banda avanza sola: mira las cámaras y escucha la radio.' : ''}</div>`;
    if (html !== reqSig) { reqSig = html; box.innerHTML = html; }
    S.requests.forEach(r => {
      if (r.timer == null) return;
      const el = box.querySelector(`[data-key="${CSS.escape(r.key)}"] .rbar i`);
      if (el) el.style.width = r.timer * 100 + '%';
    });
  }

  // ---------- Sala que se esta viendo ----------
  let roomSig = '';
  function renderRoom() {
    const R = E.ROOM[S.view], rs = S.rooms[R.id];
    const info = [];
    if (R.watched) {
      if (rs.loopT > 0) info.push(`<span class="cy">Cámara en bucle ${Math.ceil(rs.loopT)} s</span>`);
      else if (E.castroWatching()) info.push(rs.lit ? '<span class="bad">Castro vigila esta cámara</span>' : 'Castro no ve nada: sala a oscuras');
      else info.push('<span class="good">Nadie mira los monitores</span>');
    } else info.push('Cámara sin vigilancia');
    if (!R.exterior) info.push(rs.lit ? 'Luz encendida' : '<span class="bad">Luz apagada</span>');
    const acts = [];
    if (R.watched) acts.push(btn({ act: 'loop', arg: R.id, label: 'BUCLE DE CÁMARA', sub: 'hackeo corto · 2 min' }, 'act'));
    if (!R.exterior) acts.push(btn({ act: 'lights', arg: R.id, label: rs.lit ? 'APAGAR LUZ' : 'ENCENDER LUZ', sub: rs.lit ? 'ciega a todos (L)' : 'vuelve la luz (L)' }, 'act'));
    if (R.phone) acts.push(btn({ act: 'phone', arg: R.id, label: 'HACER SONAR TELÉFONO', sub: 'atrae al guardia cercano (T)' }, 'act'));
    for (const d of R.doorDefs) {
      const ds = S.doors[d.id];
      if (!ds.locked || d.vault) continue;
      acts.push(btn({ act: 'door', arg: d.id, label: 'ABRIR ' + d.name.toUpperCase(), sub: 'hackeo corto' }, 'act'));
    }
    acts.push(`<button class="act" data-act="ir">${S.ir ? 'INFRARROJOS: SÍ' : 'INFRARROJOS: NO'}<small>ver a oscuras (I)</small></button>`);
    const html = `<div class="rname">CAM ${R.cam} · ${R.name}</div><div class="rinfo">${info.join(' · ')}</div><div class="acts">${acts.join('')}</div>`;
    if (html !== roomSig) { roomSig = html; $('#roomPanel').innerHTML = html; }
  }

  // ---------- Tira de camaras ----------
  function buildCams() {
    $('#camStrip').innerHTML = E.ROOMS.map(r => `<button class="cam" data-act="view" data-arg="${r.id}"><span class="k">${r.cam}</span>${r.name}</button>`).join('')
      + '<button class="cam auto" data-act="auto" title="La cámara sigue a la banda cuando cambia de sala">SEGUIR BANDA</button>';
  }
  function renderCams() {
    document.querySelectorAll('#camStrip .cam').forEach(b => {
      const id = b.dataset.arg;
      if (b.dataset.act === 'auto') { b.classList.toggle('on', S.autoFollow); return; }
      const rs = S.rooms[id];
      b.classList.toggle('on', S.view === id);
      b.classList.toggle('dark', !rs.lit);
      b.classList.toggle('looped', rs.loopT > 0);
      const hasCrew = S.crew.some(c => c.room === id);
      const dot = b.querySelector('.dot');
      if (hasCrew && !dot) b.insertAdjacentHTML('beforeend', '<span class="dot"></span>');
      if (!hasCrew && dot) dot.remove();
    });
  }

  // ---------- HUD de la imagen ----------
  function renderHud() {
    const R = E.ROOM[S.view], rs = S.rooms[R.id];
    $('#hudCam').textContent = `CAM ${pad(R.cam)} · ${R.name}`;
    $('#hudTime').textContent = clock(S.t);
    $('#clock').textContent = clock(S.t);
    const st = [];
    if (S.ir) st.push('<span class="ir">IR</span>');
    if (!rs.lit && !S.ir) st.push('<span class="dark">SIN LUZ · pulsa I</span>');
    if (R.watched && rs.lit && !rs.loopT && E.castroWatching()) st.push('<span class="watch">● VIGILADA</span>');
    $('#hudStatus').innerHTML = st.join(' ');
    $('#hudLoop').innerHTML = rs.loopT > 0 ? `<span class="loop">BUCLE ${Math.floor(rs.loopT / 60)}:${pad(Math.ceil(rs.loopT) % 60)}</span>` : '';
  }

  // ---------- Plano ----------
  const MS = 20;
  let mctx;
  function drawMap() {
    const c = mctx, W = C.MAP_W * MS, H = C.MAP_H * MS;
    c.fillStyle = '#05080a'; c.fillRect(0, 0, W, H);
    for (const R of E.ROOMS) {
      const rs = S.rooms[R.id];
      c.fillStyle = R.exterior ? '#10161a' : rs.lit ? '#17272a' : '#07090b';
      c.fillRect(R.x * MS, R.y * MS, R.w * MS, R.h * MS);
    }
    c.fillStyle = 'rgba(70,95,95,0.35)';
    for (const o of E.OBJECTS) c.fillRect(o.x * MS + 2, o.y * MS + 2, o.w * MS - 4, o.d * MS - 4);
    for (const R of E.ROOMS) {
      const rs = S.rooms[R.id], on = S.view === R.id;
      c.strokeStyle = on ? '#ffcc55' : '#2d4a4c'; c.lineWidth = on ? 3 : 2;
      c.strokeRect(R.x * MS + 1, R.y * MS + 1, R.w * MS - 2, R.h * MS - 2);
      c.font = '18px VT323, monospace'; c.textBaseline = 'top'; c.textAlign = 'left';
      c.fillStyle = on ? '#ffcc55' : rs.lit || R.exterior ? '#8fb3a5' : '#3c5550';
      c.fillText(`${R.cam} ${R.name}`, R.x * MS + 5, R.y * MS + 3);
      let tag = '', col = '';
      if (rs.loopT > 0) { tag = 'BUCLE'; col = '#55d6e6'; }
      else if (R.watched && rs.lit && E.castroWatching()) { tag = 'VIGILADA'; col = '#ff7a6a'; }
      if (!rs.lit && !R.exterior) { tag = (tag ? tag + ' · ' : '') + 'SIN LUZ'; col = col || '#7a8a86'; }
      if (tag) { c.fillStyle = col; c.textAlign = 'right'; c.fillText(tag, (R.x + R.w) * MS - 5, R.y * MS + 3); c.textAlign = 'left'; }
      if (R.phone) { c.fillStyle = S.ringRoom === R.id && S.ringT > 0 ? '#ffe066' : '#6f8a80'; c.fillText('☎', R.phone.x * MS - 14, R.phone.y * MS + 2); }
    }
    for (const d of E.DOORS) {
      const ds = S.doors[d.id];
      c.fillStyle = d.vault ? (ds.locked ? '#e0b040' : '#3fbf7f') : ds.locked ? '#d0453c' : '#3fbf7f';
      c.fillRect(d.x * MS + 4, d.y * MS + 4, MS - 8, MS - 8);
    }
    const dot = (a, col, letter) => {
      const x = a.x * MS, y = a.y * MS;
      c.fillStyle = col; c.beginPath(); c.arc(x, y, 7, 0, Math.PI * 2); c.fill();
      c.fillStyle = '#05080a'; c.font = '16px VT323, monospace'; c.textAlign = 'center'; c.textBaseline = 'middle'; c.fillText(letter, x, y + 1);
    };
    for (const g of S.guards) {
      const seen = S.rooms[g.room].lit || (S.ir && S.view === g.room);
      if (!seen) continue;
      if (g.downed) { c.strokeStyle = '#8a8a8a'; c.lineWidth = 2; const x = g.x * MS, y = g.y * MS; c.beginPath(); c.moveTo(x - 5, y - 5); c.lineTo(x + 5, y + 5); c.moveTo(x + 5, y - 5); c.lineTo(x - 5, y + 5); c.stroke(); continue; }
      c.strokeStyle = 'rgba(255,90,74,0.5)'; c.lineWidth = 2; c.beginPath(); c.moveTo(g.x * MS, g.y * MS); c.lineTo((g.x + g.fx * 1.2) * MS, (g.y + g.fy * 1.2) * MS); c.stroke();
      dot(g, g.state === 'normal' ? '#ff5a4a' : '#ff2a1a', g.name[0]);
    }
    for (const a of S.crew) dot(a, '#ffb347', a.name[0]);
  }

  // ---------- Pantallas ----------
  function titleCard() {
    $('#card').innerHTML = `
      <h1>THE EYE IN THE SKY</h1>
      <h2>Golpe en Industrial 14 · 02:15 de la madrugada</h2>
      <p>Eres el hacker de la furgoneta. Nunca sales de ella y nadie te ve.
      <b>Vito</b> y <b>Nora</b> entran solos: eligen el camino, esperan cuando hay peligro y noquean guardias cuando pueden.
      Tú les abres paso desde las cámaras.</p>
      <ul>
        <li>Cuando la banda necesita algo, aparece en <b>PETICIONES</b> (arriba a la derecha). Pulsa el botón y resuelve el hackeo.</li>
        <li>Los hackeos son cortos y, mientras hackeas, el edificio va a cámara lenta.</li>
        <li>Apagar la luz ciega a todos: guardias, cámaras y también a ti. Con <b>I</b> ves en infrarrojos.</li>
        <li>Un teléfono que suena atrae al guardia más cercano.</li>
        <li>La policía no sale en pantalla. Solo la oyes por el escáner: si mandan una unidad, date prisa.</li>
      </ul>
      <p class="keys">1-7 cambiar cámara · I infrarrojos · L luz · T teléfono · en los hackeos: ESPACIO, flechas, números o ratón · ESC deja el hackeo sin penalización</p>
      <div class="row">
        <span>DIFICULTAD</span>
        <span class="seg"><button data-tune="normal" class="${tune === 'normal' ? 'on' : ''}">NORMAL</button><button data-tune="relaxed" class="${tune === 'relaxed' ? 'on' : ''}">RELAJADA</button></span>
        <span style="flex:1"></span>
        <button class="big" id="btnStart">EMPEZAR</button>
      </div>
      <p class="keys">Las voces son la síntesis de voz del navegador. Puedes quitarlas arriba con VOZ.</p>`;
    $('#overlay').hidden = false;
  }
  function helpCard() {
    S.paused = true;
    $('#card').innerHTML = `
      <h1>AYUDA</h1><h2>Partida en pausa</h2>
      <ul>
        <li><b>La banda va sola.</b> No hace falta darles órdenes. Si se paran, es porque esperan algo: mira PETICIONES.</li>
        <li><b>Puertas cerradas:</b> ábrelas antes de que lleguen y no tendrán que esperar a la vista.</li>
        <li><b>Cámaras vigiladas:</b> Castro mira los monitores desde SEGURIDAD. Un bucle o un apagón le dejan ciego. Si Castro sale de su puesto, nadie vigila.</li>
        <li><b>Guardias:</b> la banda espera si hay uno delante. Si la sala está a oscuras, Vito lo noquea solo. Un teléfono que suena lo aparta.</li>
        <li><b>Guardias noqueados:</b> no contestan a los controles por radio. Cuando pase, suplanta su voz desde PETICIONES.</li>
        <li><b>Policía:</b> cada error suma, pero nunca ves cuánto. El escáner te dice si mandan a alguien y lo cerca que está.</li>
      </ul>
      <div class="row"><span style="flex:1"></span><button class="big" id="btnResume">SEGUIR</button></div>`;
    $('#overlay').hidden = false;
  }
  function endCard() {
    const P = S.police, won = S.over === 'win';
    const tl = S.timeline.map(e => `<div class="${e.important ? 'imp' : ''}"><span class="ts">${clock(e.t).slice(0, 5)}</span>${esc(e.text)}${e.add ? ` <span class="add">+${Math.round(e.add)}</span>` : ''}</div>`).join('');
    $('#card').innerHTML = `
      <h1>${won ? 'GOLPE COMPLETADO' : 'LA POLICÍA HA LLEGADO'}</h1>
      <h2>${won ? (P.state >= 2 ? `Os fuisteis con ${P.unit === 'UNIDAD 14' ? 'la Unidad 14' : 'la Unidad 7'} de camino.` : 'Nadie se enteró de nada.') : 'La banda sigue dentro cuando llega la patrulla.'}</h2>
      <div class="stats">
        <div class="stat"><b>${clock(S.t).slice(0, 5)}</b>hora final</div>
        <div class="stat"><b>${S.stats.hacksOk}</b>hackeos logrados</div>
        <div class="stat"><b>${S.stats.attemptsLost}</b>intentos fallidos</div>
        <div class="stat"><b>${S.stats.takedowns}</b>guardias noqueados</div>
      </div>
      ${S.betrayedBy ? `<p class="verdict">Lo que hizo salir a la policía: ${esc(S.betrayedBy)}.</p>` : '<p>La policía nunca salió hacia el edificio.</p>'}
      <div class="ptitle">LO QUE SE OYÓ Y LO QUE SUMÓ SOSPECHA</div>
      <div class="tl-list">${tl || '<div>Nada. Un golpe limpio.</div>'}</div>
      <div class="row"><span style="flex:1"></span><button class="big" id="btnAgain">OTRA VEZ</button></div>`;
    $('#overlay').hidden = false;
  }
  function startGame() {
    E.audio.init();
    $('#log').innerHTML = '';
    S = E.newGame(tune);
    reqSig = roomSig = '';
    $('#overlay').hidden = true;
    E.start();
  }

  // ---------- Entrada ----------
  function onClick(e) {
    const t = e.target.closest('[data-act],[data-tune],#btnStart,#btnResume,#btnAgain,#btnHelp,#btnVoice');
    if (!t) return;
    if (t.id === 'btnStart' || t.id === 'btnAgain') { startGame(); return; }
    if (t.id === 'btnResume') { S.paused = false; $('#overlay').hidden = true; return; }
    if (t.id === 'btnHelp') { if (S.started && !S.over) helpCard(); return; }
    if (t.id === 'btnVoice') { E.audio.setVoice(!E.audio.voice); t.textContent = E.audio.voice ? 'VOZ: SÍ' : 'VOZ: NO'; return; }
    if (t.dataset.tune) { tune = t.dataset.tune; titleCard(); return; }
    if (t.disabled) return;
    const act = t.dataset.act, arg = t.dataset.arg;
    if (act === 'ir') { S.ir = !S.ir; return; }
    if (act === 'auto') { S.autoFollow = !S.autoFollow; if (S.autoFollow) { S.manualT = -99; E.setView(S.vito.room); } return; }
    E.act(act, arg);
  }
  function onKey(e) {
    const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
    E.input.down[k] = true;
    if (!$('#overlay').hidden) {
      if (k === 'Enter') { const b = $('#btnStart') || $('#btnAgain') || $('#btnResume'); if (b) b.click(); }
      return;
    }
    if (!S.started || S.over) return;
    if (S.hack) {
      if (k === 'Escape') E.hack.cancel(); else E.hack.key(k);
      if (k === ' ' || k.startsWith('Arrow')) e.preventDefault();
      return;
    }
    if (k >= '1' && k <= '7') E.act('view', E.ROOMS[+k - 1].id);
    else if (k === 'i') S.ir = !S.ir;
    else if (k === 'l' && !E.ROOM[S.view].exterior) E.act('lights', S.view);
    else if (k === 't' && E.ROOM[S.view].phone) E.act('phone', S.view);
    else if (k === 'Escape' || k === 'h') helpCard();
  }
  function onMap(e) {
    const r = e.target.getBoundingClientRect();
    const x = Math.floor(((e.clientX - r.left) / r.width) * C.MAP_W), y = Math.floor(((e.clientY - r.top) / r.height) * C.MAP_H);
    const did = E.doorIdAt(x, y);
    if (did && S.doors[did].locked && !S.doors[did].def.vault) { E.act('door', did); return; }
    const id = E.roomAt(x, y);
    if (id) E.act('view', id);
  }

  // ---------- Bucle ----------
  let last = 0, uiT = 0;
  function frame(ts) {
    const dt = Math.min(0.05, (ts - last) / 1000 || 0);
    last = ts;
    if (S.started && !S.paused) {
      E.sim.update(dt * (S.hack ? E.TUNE.slowmo : 1));
      if (S.hack) E.hack.update(dt);
      if (S.over && S.endT > 3 && $('#overlay').hidden && !S.endShown) { S.endShown = true; endCard(); }
    }
    E.render.draw(dt);
    E.hack.draw();
    E.audio.update();
    toastT -= dt;
    if (toastT <= 0) $('#toast').classList.remove('on');
    uiT -= dt;
    if (uiT <= 0) { uiT = 0.1; renderHud(); renderRequests(); renderRoom(); renderCams(); drawMap(); }
    requestAnimationFrame(frame);
  }

  ui.refresh = () => { renderHud(); renderRequests(); renderRoom(); renderCams(); drawMap(); };
  ui.endCard = () => endCard();

  function boot() {
    const m = $('#map');
    m.width = C.MAP_W * MS; m.height = C.MAP_H * MS;
    mctx = m.getContext('2d');
    E.render.init($('#feed'));
    E.hack.init();
    buildCams();
    S = E.newGame(tune);
    document.addEventListener('click', onClick);
    document.addEventListener('keydown', onKey);
    document.addEventListener('keyup', e => { const k = e.key.length === 1 ? e.key.toLowerCase() : e.key; delete E.input.down[k]; });
    window.addEventListener('blur', () => { E.input.down = {}; });
    m.addEventListener('click', onMap);
    titleCard();
    requestAnimationFrame(frame);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot); else boot();
})();
