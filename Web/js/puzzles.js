'use strict';
// Hackeos cortos. Cada uno dura segundos y el mundo va a camara lenta mientras tanto.
(function () {
  const E = window.EYE;
  const PW = 400, PH = 200;
  const COL = { bg: '#04090a', grid: '#0b231d', fg: '#5cf2a4', dim: '#2b6b52', warn: '#ffcc55', bad: '#ff5a4a', cyan: '#55d6e6', white: '#e8fff2' };
  const rnd = n => Math.floor(Math.random() * n);
  const pick = a => a[rnd(a.length)];
  function shuffle(a) { for (let i = a.length - 1; i > 0; i--) { const j = rnd(i + 1); [a[i], a[j]] = [a[j], a[i]]; } return a; }
  const keyDown = k => !!E.input.down[k];

  function bg(c) {
    c.fillStyle = COL.bg; c.fillRect(0, 0, PW, PH);
    c.strokeStyle = COL.grid; c.lineWidth = 1;
    for (let x = 0; x <= PW; x += 20) { c.beginPath(); c.moveTo(x + 0.5, 0); c.lineTo(x + 0.5, PH); c.stroke(); }
    for (let y = 0; y <= PH; y += 20) { c.beginPath(); c.moveTo(0, y + 0.5); c.lineTo(PW, y + 0.5); c.stroke(); }
  }
  function text(c, s, x, y, col, size, align) {
    c.font = `${size || 20}px VT323, ui-monospace, monospace`;
    c.fillStyle = col; c.textAlign = align || 'left'; c.textBaseline = 'middle';
    c.fillText(s, x, y);
  }

  // ---------- SINCRONIA (puertas): parar la aguja en la zona ----------
  function Sync() {
    const X0 = 40, X1 = 360;
    const p = { title: 'SINCRONÍA', help: 'Pulsa ESPACIO o haz clic cuando la aguja esté dentro de la zona verde. Tres aciertos.', time: 14, hits: 0, need: 3, pos: 0, dir: 1, flash: 0, flashOk: false };
    function zone() { p.zw = 52 - p.hits * 8; p.zx = X0 + 20 + Math.random() * (X1 - X0 - 40 - p.zw); }
    zone();
    p.update = dt => {
      const sp = 150 + p.hits * 30;
      p.pos += p.dir * sp * dt;
      if (p.pos > X1 - X0) { p.pos = X1 - X0; p.dir = -1; }
      if (p.pos < 0) { p.pos = 0; p.dir = 1; }
      p.flash = Math.max(0, p.flash - dt);
    };
    p.press = () => {
      const x = X0 + p.pos;
      if (x >= p.zx - 2 && x <= p.zx + p.zw + 2) {
        p.hits++; p.flash = 0.25; p.flashOk = true; E.audio.tick();
        if (p.hits >= p.need) p.result = 'ok'; else zone();
      } else { p.flash = 0.35; p.flashOk = false; p.missed = true; }
    };
    p.key = k => { if (k === ' ' || k === 'Enter') p.press(); };
    p.click = () => p.press();
    p.draw = c => {
      bg(c);
      const y = 100;
      text(c, 'CERRADURA ELECTRÓNICA', PW / 2, 36, COL.dim, 22, 'center');
      c.fillStyle = '#0f1f1a'; c.fillRect(X0, y - 16, X1 - X0, 32);
      c.fillStyle = COL.dim; c.fillRect(p.zx, y - 16, p.zw, 32);
      c.fillStyle = COL.fg; c.fillRect(p.zx, y - 16, p.zw, 3); c.fillRect(p.zx, y + 13, p.zw, 3);
      const nx = X0 + p.pos;
      c.fillStyle = p.flash > 0 ? (p.flashOk ? COL.white : COL.bad) : COL.warn;
      c.fillRect(nx - 2, y - 26, 4, 52);
      for (let i = 0; i < p.need; i++) { c.fillStyle = i < p.hits ? COL.fg : '#15302a'; c.fillRect(PW / 2 - 52 + i * 38, 152, 28, 12); }
    };
    p.solve = () => { p.result = 'ok'; };
    return p;
  }

  // ---------- BUCLE (camaras): alinear dos ondas ----------
  function Loop() {
    const p = { title: 'BUCLE DE VÍDEO', help: 'Alinea tu onda (amarilla) con la de la cámara: flechas ← → o mueve el ratón. Cuando encaje, ESPACIO o clic.', time: 14, t: 0 };
    p.target = Math.random() * Math.PI * 2;
    p.phase = p.target + Math.PI * (0.6 + Math.random() * 0.6);
    p.flash = 0;
    const match = () => (Math.cos(p.phase - p.target) + 1) / 2;
    p.update = dt => {
      p.t += dt;
      p.target += Math.sin(p.t * 0.8) * 0.45 * dt;
      if (keyDown('ArrowLeft') || keyDown('a')) p.phase -= 2.2 * dt;
      if (keyDown('ArrowRight') || keyDown('d')) p.phase += 2.2 * dt;
      p.flash = Math.max(0, p.flash - dt);
    };
    p.press = () => { if (match() >= 0.92) p.result = 'ok'; else { p.missed = true; p.flash = 0.35; } };
    p.key = k => { if (k === ' ' || k === 'Enter') p.press(); };
    p.mouse = x => { p.phase = (x / PW) * Math.PI * 4; };
    p.click = () => p.press();
    p.draw = c => {
      bg(c);
      const mid = 92, A = 42, k = (Math.PI * 4) / (PW - 40), m = match();
      const wave = (ph, col, w) => {
        c.strokeStyle = col; c.lineWidth = w; c.beginPath();
        for (let x = 20; x <= PW - 20; x += 3) { const y = mid + A * Math.sin(k * (x - 20) + ph); if (x === 20) c.moveTo(x, y); else c.lineTo(x, y); }
        c.stroke();
      };
      wave(p.target, COL.dim, 6);
      wave(p.phase, p.flash > 0 ? COL.bad : m >= 0.92 ? COL.fg : COL.warn, 2);
      c.fillStyle = '#0f1f1a'; c.fillRect(60, 160, 280, 10);
      c.fillStyle = m >= 0.92 ? COL.fg : COL.warn; c.fillRect(60, 160, 280 * m, 10);
      c.fillStyle = COL.white; c.fillRect(60 + 280 * 0.92, 156, 2, 18);
      text(c, m >= 0.92 ? 'ENCAJA: PULSA' : `SINCRONÍA ${Math.round(m * 100)}%`, PW / 2, 184, m >= 0.92 ? COL.fg : COL.dim, 20, 'center');
    };
    p.solve = () => { p.result = 'ok'; };
    return p;
  }

  // ---------- CRIPTA corta (terminal): memorizar 4 simbolos ----------
  const GLYPHS = [
    '00100/01110/11111/01110/00100', '11111/10001/10101/10001/11111', '10001/01010/00100/01010/10001',
    '00100/00100/11111/00100/00100', '01110/10001/10001/10001/01110', '00100/01010/10001/11111/00000',
    '11011/11011/00000/11011/11011', '10000/11000/11100/11110/11111', '01010/11111/01010/11111/01010',
    '00100/01110/10101/00100/00100', '11111/00001/01111/01000/01111', '11100/00100/00111/00100/11100',
  ].map(s => s.split('/'));
  function glyph(c, gi, x, y, s, col) {
    c.fillStyle = col;
    const g = GLYPHS[gi];
    for (let r = 0; r < 5; r++) for (let q = 0; q < 5; q++) if (g[r][q] === '1') c.fillRect(x + q * s, y + r * s, s - 1, s - 1);
  }
  function Crypt() {
    const all = shuffle([...Array(GLYPHS.length).keys()]);
    const seq = all.slice(0, 4);
    const opts = shuffle(all.slice(0, 8));
    const p = { title: 'CÓDIGO DE LA BÓVEDA', help: 'Memoriza los 4 símbolos. Luego márcalos en el mismo orden con 1-8 o con clic.', time: 20, phase: 'show', showT: 3.2, prog: 0, wrong: -1, wrongT: 0 };
    const rects = [];
    p.update = dt => {
      if (p.phase === 'show') { p.showT -= dt; if (p.showT <= 0) p.phase = 'input'; }
      p.wrongT = Math.max(0, p.wrongT - dt);
    };
    function choose(i) {
      if (p.phase !== 'input' || i < 0 || i >= 8) return;
      if (opts[i] === seq[p.prog]) { p.prog++; E.audio.tick(); if (p.prog >= 4) p.result = 'ok'; }
      else { p.missed = true; p.wrong = i; p.wrongT = 0.5; p.prog = 0; p.phase = 'show'; p.showT = 2.2; }
    }
    p.key = k => { if (k === ' ' && p.phase === 'show') { p.showT = 0; return; } const n = parseInt(k, 10); if (n >= 1 && n <= 8) choose(n - 1); };
    p.click = (x, y) => { for (let i = 0; i < rects.length; i++) { const r = rects[i]; if (x >= r[0] && x <= r[0] + r[2] && y >= r[1] && y <= r[1] + r[3]) choose(i); } };
    p.draw = c => {
      bg(c);
      if (p.phase === 'show') {
        text(c, 'MEMORIZA', PW / 2, 30, COL.warn, 24, 'center');
        for (let i = 0; i < 4; i++) {
          const x = 70 + i * 72;
          c.strokeStyle = COL.dim; c.strokeRect(x - 6.5, 62.5, 60, 60);
          glyph(c, seq[i], x + 2, 70, 9, COL.fg);
          text(c, String(i + 1), x + 24, 140, COL.dim, 20, 'center');
        }
        c.fillStyle = COL.dim; c.fillRect(70, 165, 260 * Math.max(0, p.showT / 3.2), 4);
        text(c, 'ESPACIO para empezar ya', PW / 2, 184, COL.dim, 18, 'center');
        return;
      }
      text(c, `ORDEN ${p.prog}/4`, PW / 2, 22, COL.warn, 22, 'center');
      for (let i = 0; i < 4; i++) {
        const x = 128 + i * 38;
        c.strokeStyle = COL.dim; c.strokeRect(x + 0.5, 38.5, 30, 30);
        if (i < p.prog) glyph(c, seq[i], x + 5, 43, 4, COL.fg);
      }
      rects.length = 0;
      for (let i = 0; i < 8; i++) {
        const col = i % 4, row = (i / 4) | 0, x = 48 + col * 80, y = 82 + row * 58;
        rects.push([x, y, 64, 52]);
        const bad = p.wrongT > 0 && p.wrong === i;
        c.fillStyle = bad ? '#3a1210' : '#0b1a15'; c.fillRect(x, y, 64, 52);
        c.strokeStyle = bad ? COL.bad : COL.dim; c.strokeRect(x + 0.5, y + 0.5, 64, 52);
        glyph(c, opts[i], x + 20, y + 10, 6, bad ? COL.bad : COL.white);
        text(c, String(i + 1), x + 6, y + 9, COL.dim, 18);
      }
    };
    p.solve = () => { p.result = 'ok'; };
    return p;
  }

  // ---------- HUELLA (boveda, parte 1): elegir la huella que coincide ----------
  function fpCanvas(pr) {
    const w = 60, h = 72;
    const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
    const c = cv.getContext('2d'), im = c.createImageData(w, h), d = im.data;
    let seed = pr.seed;
    const rand = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };
    const scars = [];
    for (let i = 0; i < 3; i++) scars.push([rand() * 1.6 - 0.8, rand() * 1.8 - 0.9, rand() * 3]);
    for (let py = 0; py < h; py++) for (let px = 0; px < w; px++) {
      const u = (px / (w - 1) - 0.5) * 2, v = (py / (h - 1) - 0.5) * 2, j = (py * w + px) * 4;
      const e = (u / 0.9) ** 2 + (v / 1.0) ** 2;
      let ink = false;
      if (e < 1) {
        const dx = u - pr.cx, dy = v - pr.cy;
        let val;
        if (pr.type === 0) { const r = Math.hypot(dx, dy), a = Math.atan2(dy, dx); val = Math.sin(r * pr.f + a * pr.tw); }
        else if (pr.type === 1) val = Math.sin(pr.f * Math.hypot(dx, dy * 1.7 - 0.45 * dx * dx * (1 + pr.tw * 0.5)));
        else val = Math.sin(pr.f * 0.6 * (dy * 1.6 + 0.8 * Math.exp(-dx * dx * 3) * (1 + 0.35 * pr.tw)));
        if (e > 0.82) val -= (e - 0.82) * 4;
        ink = val > 0.15;
        for (const s of scars) if (Math.abs((u - s[0]) * Math.cos(s[2]) + (v - s[1]) * Math.sin(s[2])) < 0.035 && Math.hypot(u - s[0], v - s[1]) < 0.35) ink = false;
      }
      const col = ink ? [214, 245, 228] : e < 1 ? [18, 40, 33] : [6, 14, 12];
      d[j] = col[0]; d[j + 1] = col[1]; d[j + 2] = col[2]; d[j + 3] = 255;
    }
    c.putImageData(im, 0, 0);
    return cv;
  }
  function Print() {
    const type = rnd(3);
    const params = t => ({ type: t, f: 16 + Math.random() * 5, tw: t === 0 ? pick([1, 2, 3]) : Math.random() * 2 - 1, cx: (Math.random() - 0.5) * 0.3, cy: (Math.random() - 0.5) * 0.3, seed: rnd(1e9) });
    const base = params(type);
    const twist = Object.assign({}, base, { tw: type === 0 ? -base.tw : base.tw + (base.tw > 0 ? -1.4 : 1.4), seed: rnd(1e9) });
    const freq = Object.assign({}, base, { f: base.f + (base.f < 18 ? 7 : -6), cx: base.cx + 0.22, seed: rnd(1e9) });
    const other = params((type + 1 + rnd(2)) % 3);
    const cands = shuffle([{ p: base, ok: true }, { p: twist }, { p: freq }, { p: other }]);
    cands.forEach(k => { k.cv = fpCanvas(k.p); });
    const target = fpCanvas(base);
    const tmp = document.createElement('canvas'), tctx = tmp.getContext('2d');
    const p = { title: 'HUELLA DACTILAR', help: 'La muestra del lector (izquierda) coincide con una de las cuatro. Se enfocan solas: elige con 1-4 o clic.', time: 18, t: 0, crossed: [] };
    const rects = [];
    p.update = dt => { p.t += dt; };
    function choose(i) {
      if (i < 0 || i > 3 || p.crossed[i]) return;
      if (cands[i].ok) p.result = 'ok'; else { p.crossed[i] = true; p.missed = true; }
    }
    p.key = k => { const n = parseInt(k, 10); if (n >= 1 && n <= 4) choose(n - 1); };
    p.click = (x, y) => { rects.forEach((r, i) => { if (x >= r[0] && x <= r[0] + r[2] && y >= r[1] && y <= r[1] + r[3]) choose(i); }); };
    p.draw = c => {
      bg(c);
      c.imageSmoothingEnabled = false;
      text(c, 'MUESTRA', 58, 24, COL.warn, 20, 'center');
      c.drawImage(target, 18, 40, 80, 96);
      c.strokeStyle = COL.warn; c.strokeRect(17.5, 39.5, 81, 97);
      const block = Math.max(1, Math.round(8 - p.t * 1.9));
      text(c, block > 1 ? 'ENFOCANDO...' : 'ENFOCADO', 260, 24, block > 1 ? COL.dim : COL.fg, 20, 'center');
      rects.length = 0;
      for (let i = 0; i < 4; i++) {
        const x = 130 + i * 66, y = 44;
        rects.push([x, y, 58, 110]);
        const sw = Math.ceil(60 / block), sh = Math.ceil(72 / block);
        tmp.width = sw; tmp.height = sh;
        tctx.imageSmoothingEnabled = true;
        tctx.drawImage(cands[i].cv, 0, 0, sw, sh);
        c.drawImage(tmp, 0, 0, sw, sh, x, y, 58, 70);
        c.strokeStyle = p.crossed[i] ? COL.bad : COL.dim; c.strokeRect(x - 0.5, y - 0.5, 59, 71);
        if (p.crossed[i]) { c.strokeStyle = COL.bad; c.lineWidth = 2; c.beginPath(); c.moveTo(x, y); c.lineTo(x + 58, y + 70); c.moveTo(x + 58, y); c.lineTo(x, y + 70); c.stroke(); c.lineWidth = 1; }
        text(c, String(i + 1), x + 29, y + 88, p.crossed[i] ? COL.bad : COL.white, 24, 'center');
      }
      c.imageSmoothingEnabled = true;
    };
    p.solve = () => { p.result = 'ok'; };
    return p;
  }

  // ---------- CORTAFUEGOS (boveda, parte 2): laberinto con perseguidor lento ----------
  function Firewall() {
    const CW = 9, CH = 5, GW = CW * 2 + 1, GH = CH * 2 + 1, T = 16;
    const OX = Math.round((PW - GW * T) / 2), OY = Math.round((PH - GH * T) / 2) + 4;
    const g = new Uint8Array(GW * GH).fill(1);
    const at = (x, y) => g[y * GW + x];
    const seen = new Uint8Array(CW * CH);
    const stack = [[0, 0]];
    seen[0] = 1; g[1 * GW + 1] = 0;
    while (stack.length) {
      const [cx, cy] = stack[stack.length - 1];
      const nb = [[1, 0], [-1, 0], [0, 1], [0, -1]].map(([dx, dy]) => [cx + dx, cy + dy, dx, dy]).filter(([x, y]) => x >= 0 && y >= 0 && x < CW && y < CH && !seen[y * CW + x]);
      if (!nb.length) { stack.pop(); continue; }
      const [nx, ny, dx, dy] = pick(nb);
      seen[ny * CW + nx] = 1;
      g[(cy * 2 + 1 + dy) * GW + (cx * 2 + 1 + dx)] = 0;
      g[(ny * 2 + 1) * GW + (nx * 2 + 1)] = 0;
      stack.push([nx, ny]);
    }
    for (let i = 0; i < 16; i++) {
      const x = 1 + rnd(GW - 2), y = 1 + rnd(GH - 2);
      if ((x + y) % 2 === 1 && at(x, y)) g[y * GW + x] = 0;
    }
    function bfs(sx, sy) {
      const dd = new Int16Array(GW * GH).fill(-1), q = [[sx, sy]];
      dd[sy * GW + sx] = 0;
      for (let i = 0; i < q.length; i++) {
        const [x, y] = q[i];
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= GW || ny >= GH || at(nx, ny) || dd[ny * GW + nx] >= 0) continue;
          dd[ny * GW + nx] = dd[y * GW + x] + 1; q.push([nx, ny]);
        }
      }
      return dd;
    }
    const start = { x: 1, y: 1 }, exit = { x: GW - 2, y: GH - 2 }, cStart = { x: GW - 2, y: 1 };
    const dStart = bfs(1, 1), dExit = bfs(exit.x, exit.y);
    const cells = [];
    for (let y = 1; y < GH; y += 2) for (let x = 1; x < GW; x += 2) if (dStart[y * GW + x] >= 8 && dExit[y * GW + x] >= 4 && !(x === cStart.x && y === cStart.y)) cells.push({ x, y });
    shuffle(cells);
    const keys = cells.slice(0, 2).map(k => Object.assign(k, { got: false }));
    const p = { title: 'CORTAFUEGOS', help: 'Mueve tu sonda con las flechas (o WASD). Recoge las 2 llaves amarillas y sal por la esquina. Que no te toque el rastreador rojo.', time: 40, t: 0, rep: 0, acc: 0, delay: 2.2, flash: 0 };
    p.pl = { ...start }; p.ch = { ...cStart };
    const DIRK = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1], a: [-1, 0], d: [1, 0], w: [0, -1], s: [0, 1] };
    function move(dx, dy) {
      const nx = p.pl.x + dx, ny = p.pl.y + dy;
      if (nx < 0 || ny < 0 || nx >= GW || ny >= GH || at(nx, ny)) return;
      p.pl.x = nx; p.pl.y = ny;
      for (const k of keys) if (!k.got && k.x === nx && k.y === ny) { k.got = true; E.audio.tick(); }
      if (keys.every(k => k.got) && nx === exit.x && ny === exit.y) p.result = 'ok';
      caught();
    }
    function caught() {
      if (p.ch.x === p.pl.x && p.ch.y === p.pl.y && p.t > p.delay) {
        p.missed = true; p.flash = 0.5;
        p.pl = { ...start }; p.ch = { ...cStart }; p.delay = p.t + 1.6; p.acc = 0;
      }
    }
    p.update = dt => {
      p.t += dt; p.flash = Math.max(0, p.flash - dt);
      p.rep -= dt;
      if (p.rep <= 0) for (const k in DIRK) if (keyDown(k)) { move(...DIRK[k]); p.rep = 0.12; break; }
      if (p.t > p.delay) {
        p.acc += dt;
        const step = 0.52 - keys.filter(k => k.got).length * 0.05;
        if (p.acc >= step) {
          p.acc = 0;
          const dd = bfs(p.pl.x, p.pl.y);
          let best = null, bv = 1e9;
          for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
            const nx = p.ch.x + dx, ny = p.ch.y + dy;
            if (nx < 0 || ny < 0 || nx >= GW || ny >= GH || at(nx, ny)) continue;
            const v = dd[ny * GW + nx];
            if (v >= 0 && v < bv) { bv = v; best = [nx, ny]; }
          }
          if (best) { p.ch.x = best[0]; p.ch.y = best[1]; }
          caught();
        }
      }
    };
    p.key = k => { if (DIRK[k]) { move(...DIRK[k]); p.rep = 0.2; } };
    p.draw = c => {
      c.fillStyle = COL.bg; c.fillRect(0, 0, PW, PH);
      for (let y = 0; y < GH; y++) for (let x = 0; x < GW; x++) {
        c.fillStyle = at(x, y) ? (p.flash > 0 ? '#3a1210' : '#123a2e') : '#06110e';
        c.fillRect(OX + x * T, OY + y * T, T, T);
        if (at(x, y)) { c.fillStyle = '#1c5a45'; c.fillRect(OX + x * T, OY + y * T, T, 2); }
      }
      const open = keys.every(k => k.got);
      c.fillStyle = open ? COL.fg : '#1f3d33'; c.fillRect(OX + exit.x * T + 2, OY + exit.y * T + 2, T - 4, T - 4);
      for (const k of keys) if (!k.got) { c.fillStyle = COL.warn; c.fillRect(OX + k.x * T + 4, OY + k.y * T + 4, T - 8, T - 8); }
      c.fillStyle = COL.cyan; c.fillRect(OX + p.pl.x * T + 3, OY + p.pl.y * T + 3, T - 6, T - 6);
      const wake = p.t > p.delay;
      c.fillStyle = wake ? COL.bad : '#6a2a24'; c.fillRect(OX + p.ch.x * T + 2, OY + p.ch.y * T + 2, T - 4, T - 4);
      text(c, `LLAVES ${keys.filter(k => k.got).length}/2`, OX, OY - 10, COL.warn, 18);
      text(c, open ? 'SALIDA ABIERTA' : 'SALIDA CERRADA', OX + GW * T, OY - 10, open ? COL.fg : COL.dim, 18, 'right');
    };
    p.solve = () => { p.result = 'ok'; };
    return p;
  }

  // ---------- PARCHE (suplantar voz): pulsar la linea con el error dentro de la ventana ----------
  function Patch() {
    const CWID = 10, WX0 = 170, WX1 = 230;
    const TOK = ['mov', 'eax', 'jmp', '0x4F', 'call', 'ret', 'push', 'pop', 'xor', 'add', 'cmp', 'nop', 'ldr', 'str', '0x1A', 'r7', 'r12', 'sub', 'voz', 'pcm'];
    const bug = rnd(3);
    const lines = [0, 1, 2].map(i => {
      let s = '';
      while (s.length < 44) s += pick(TOK) + ' ';
      s = s.slice(0, 44);
      let bugAt = -1;
      if (i === bug) { bugAt = 14 + rnd(16); s = s.slice(0, bugAt) + 'ERR' + s.slice(bugAt + 3); }
      return { s, bugAt, speed: 46 + rnd(26), off: Math.random() * 400 };
    });
    const p = { title: 'PARCHE DE VOZ', help: 'Una línea lleva un error (ERR en rojo). Cuando pase por la ventana central, pulsa su número (1-3) o haz clic en ella.', time: 16, flash: 0, flashLine: -1 };
    const span = 44 * CWID;
    const baseX = (l, r) => 20 + r * span - (l.off % span);
    p.update = dt => { lines.forEach(l => { l.off += l.speed * dt; }); p.flash = Math.max(0, p.flash - dt); };
    function choose(i) {
      if (i < 0 || i > 2) return;
      const l = lines[i];
      if (l.bugAt >= 0) {
        for (let r = 0; r < 3; r++) {
          const c = baseX(l, r) + (l.bugAt + 1.5) * CWID;
          if (c >= WX0 - 4 && c <= WX1 + 4) { p.result = 'ok'; return; }
        }
      }
      p.missed = true; p.flash = 0.4; p.flashLine = i;
    }
    p.key = k => { const n = parseInt(k, 10); if (n >= 1 && n <= 3) choose(n - 1); };
    p.click = (x, y) => { const i = Math.floor((y - 46) / 44); if (y >= 46) choose(i); };
    p.draw = c => {
      bg(c);
      c.fillStyle = 'rgba(92,242,164,0.08)'; c.fillRect(WX0, 40, WX1 - WX0, 140);
      c.strokeStyle = COL.fg; c.strokeRect(WX0 + 0.5, 40.5, WX1 - WX0, 140);
      text(c, 'VENTANA', (WX0 + WX1) / 2, 26, COL.fg, 18, 'center');
      c.font = '20px VT323, ui-monospace, monospace'; c.textBaseline = 'middle'; c.textAlign = 'left';
      lines.forEach((l, i) => {
        const y = 68 + i * 44;
        if (p.flash > 0 && p.flashLine === i) { c.fillStyle = '#3a1210'; c.fillRect(0, y - 18, PW, 36); }
        c.fillStyle = COL.dim; c.fillText(String(i + 1), 6, y);
        for (let r = 0; r < 3; r++) {
          const bx = baseX(l, r);
          for (let j = 0; j < l.s.length; j++) {
            const x = bx + j * CWID;
            if (x < 22 || x > PW - 10) continue;
            const isBug = l.bugAt >= 0 && j >= l.bugAt && j < l.bugAt + 3;
            c.fillStyle = isBug ? COL.bad : COL.fg;
            c.fillText(l.s[j], x, y);
          }
        }
      });
    };
    p.solve = () => { p.result = 'ok'; };
    return p;
  }

  const MAKERS = { sync: Sync, loop: Loop, crypt: Crypt, print: Print, firewall: Firewall, patch: Patch };

  // ---------- Sesion de hackeo ----------
  let cvs, pctx;
  const $ = s => document.querySelector(s);
  function newStage() {
    const H = E.S.hack;
    H.puzzle = MAKERS[H.spec.stages[H.stage]]();
    H.puzzle.time *= E.TUNE.puzzleTime;
    H.time = H.puzzle.time;
    $('#hackTitle').textContent = H.spec.title + (H.spec.stages.length > 1 ? `  ·  FASE ${H.stage + 1}/${H.spec.stages.length}` : '');
    $('#hackName').textContent = H.puzzle.title;
    $('#hackHelp').textContent = H.puzzle.help;
  }
  function finish(ok) {
    const S = E.S, H = S.hack;
    S.hack = null;
    $('#hack').hidden = true;
    if (ok) { S.stats.hacksOk++; E.audio.ok(); H.spec.onWin && H.spec.onWin(); }
    else {
      S.stats.hacksFail++;
      E.audio.bad();
      E.raise(6, `Hackeo fallido: ${H.spec.title.toLowerCase()}`);
      S.lock[H.spec.key] = S.t + E.TUNE.lockout;
      E.toast(`Sistema bloqueado ${E.TUNE.lockout} s`);
      E.say('crew_hack_fail', {}, 0.5);
      H.spec.onFail && H.spec.onFail();
    }
  }

  E.hack = {
    init() {
      cvs = $('#hackCanvas');
      cvs.width = PW; cvs.height = PH;
      pctx = cvs.getContext('2d');
      const pos = e => { const r = cvs.getBoundingClientRect(); return [((e.clientX - r.left) / r.width) * PW, ((e.clientY - r.top) / r.height) * PH]; };
      cvs.addEventListener('mousemove', e => { const H = E.S.hack; if (H && H.puzzle.mouse) H.puzzle.mouse(...pos(e)); });
      cvs.addEventListener('mousedown', e => { const H = E.S.hack; if (H && H.puzzle.click) H.puzzle.click(...pos(e)); });
      $('#hackCancel').addEventListener('click', () => E.hack.cancel());
    },
    start(spec) {
      const S = E.S;
      if (S.hack || S.over) return false;
      const lk = S.lock[spec.key];
      if (lk && lk > S.t) { E.toast(`Sistema bloqueado: ${Math.ceil(lk - S.t)} s`); return false; }
      S.hack = { spec, stage: 0, attempts: E.TUNE.attempts, puzzle: null, time: 0, flash: 0 };
      newStage();
      $('#hack').hidden = false;
      E.audio.blip(660, 0.08);
      return true;
    },
    update(dt) {
      const S = E.S, H = S.hack;
      if (!H) return;
      const p = H.puzzle;
      p.update(dt);
      H.time -= dt;
      H.flash = Math.max(0, H.flash - dt);
      if (p.result === 'ok') {
        E.audio.tick();
        H.stage++;
        if (H.stage >= H.spec.stages.length) { finish(true); return; }
        newStage();
        return;
      }
      if (p.missed || H.time <= 0) {
        const timeout = H.time <= 0;
        p.missed = false;
        H.attempts--;
        H.flash = 0.4;
        S.stats.attemptsLost++;
        E.audio.bad();
        E.raise(3, `Intento fallido: ${H.spec.title.toLowerCase()}`);
        if (H.attempts <= 0) { finish(false); return; }
        if (timeout) H.time = p.time;
      }
    },
    key(k) { const H = E.S.hack; if (H && H.puzzle.key) H.puzzle.key(k); },
    cancel() { const S = E.S; if (!S.hack) return; S.hack = null; $('#hack').hidden = true; E.audio.blip(330, 0.08); },
    solve() { const H = E.S.hack; if (H) H.puzzle.solve(); },
    draw() {
      const H = E.S.hack;
      if (!H) return;
      H.puzzle.draw(pctx);
      const tries = $('#hackTries');
      let html = '';
      for (let i = 0; i < E.TUNE.attempts; i++) html += `<i class="${i < H.attempts ? '' : 'off'}"></i>`;
      if (tries.innerHTML !== html) tries.innerHTML = html;
      const bar = $('#hackTime');
      bar.style.width = Math.max(0, (H.time / H.puzzle.time) * 100) + '%';
      bar.className = H.time / H.puzzle.time < 0.3 ? 'low' : '';
      $('#hack').classList.toggle('miss', H.flash > 0);
    },
  };
})();
