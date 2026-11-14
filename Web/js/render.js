'use strict';
// Imagen de camara: sala isometrica en pixel art, luz real (la oscuridad ciega) y filtro CCTV.
(function () {
  const E = window.EYE, C = E.CONST;
  const FW = C.FW, FH = C.FH, HW = C.HW, HH = C.HH, WALL = C.WALL, STUB = 7;
  let cv, ctx, lightCv, lctx, vig, noise;
  const NOISE_MASK = (1 << 18) - 1;
  const V = { room: null, ox: 0, oy: 0 };
  let time = 0;

  function init(canvas) {
    cv = canvas;
    cv.width = FW; cv.height = FH;
    ctx = cv.getContext('2d', { willReadFrequently: true });
    lightCv = document.createElement('canvas');
    lightCv.width = FW; lightCv.height = FH;
    lctx = lightCv.getContext('2d');
    vig = new Float32Array(FW * FH);
    for (let y = 0; y < FH; y++) for (let x = 0; x < FW; x++) {
      const dx = (x / FW - 0.5) * 1.7, dy = (y / FH - 0.5) * 1.7;
      let v = 1.04 - 0.5 * (dx * dx + dy * dy);
      if (y % 2 === 1) v *= 0.84;
      vig[y * FW + x] = Math.max(0, v);
    }
    noise = new Int8Array(NOISE_MASK + 1);
    for (let i = 0; i < noise.length; i++) noise[i] = Math.round((Math.random() * 2 - 1) * 127);
  }

  // ---------- Utilidades ----------
  const shadeCache = new Map();
  function shade(hex, f) {
    const k = hex + f;
    let s = shadeCache.get(k);
    if (s) return s;
    const n = parseInt(hex.slice(1), 16);
    const r = Math.min(255, Math.round(((n >> 16) & 255) * f));
    const g = Math.min(255, Math.round(((n >> 8) & 255) * f));
    const b = Math.min(255, Math.round((n & 255) * f));
    s = `rgb(${r},${g},${b})`;
    shadeCache.set(k, s);
    return s;
  }
  const hash = (x, y) => { let h = (x * 374761393 + y * 668265263) | 0; h = (h ^ (h >>> 13)) * 1274126177; return ((h ^ (h >>> 16)) >>> 0) % 1000; };
  function setView(R) {
    V.room = R;
    V.ox = Math.round(FW / 2 - ((R.w - R.h) * HW) / 2);
    V.oy = Math.round(FH / 2 + 24 - ((R.w + R.h) * HH) / 2);
  }
  function P(x, y) { const lx = x - V.room.x, ly = y - V.room.y; return [V.ox + (lx - ly) * HW, V.oy + (lx + ly) * HH]; }
  const up = (p, h) => [p[0], p[1] - h];
  function poly(pts, fill, c) {
    c = c || ctx;
    c.beginPath();
    c.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length; i++) c.lineTo(pts[i][0], pts[i][1]);
    c.closePath();
    c.fillStyle = fill;
    c.fill();
  }
  function seg(a, b, col, w) { ctx.strokeStyle = col; ctx.lineWidth = w || 1; ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.lineTo(b[0], b[1]); ctx.stroke(); }
  // Caja isometrica: x,y,w,d en casillas; h y z en pixeles.
  function box(x, y, w, d, h, z, col) {
    const p00 = up(P(x, y), z), p10 = up(P(x + w, y), z), p11 = up(P(x + w, y + d), z), p01 = up(P(x, y + d), z);
    poly([p01, p11, up(p11, h), up(p01, h)], shade(col, 0.84));
    poly([p11, p10, up(p10, h), up(p11, h)], shade(col, 0.66));
    poly([up(p00, h), up(p10, h), up(p11, h), up(p01, h)], shade(col, 1.1));
  }
  // Cara sur (paralela al eje x) en y fijo, y cara este (paralela al eje y) en x fijo.
  const faceS = (x0, x1, y, z0, z1, col) => poly([up(P(x0, y), z0), up(P(x1, y), z0), up(P(x1, y), z1), up(P(x0, y), z1)], col);
  const faceE = (x, y0, y1, z0, z1, col) => poly([up(P(x, y0), z0), up(P(x, y1), z0), up(P(x, y1), z1), up(P(x, y0), z1)], col);

  // ---------- Suelo y paredes ----------
  function drawFloor(R) {
    for (let ly = 0; ly < R.h; ly++) for (let lx = 0; lx < R.w; lx++) {
      const x = R.x + lx, y = R.y + ly;
      const f = 0.95 + (hash(x, y) % 6) * 0.014;
      poly([P(x, y), P(x + 1, y), P(x + 1, y + 1), P(x, y + 1)], shade(R.floor[(lx + ly) & 1], f));
    }
    if (R.id === 'dock') {
      // franja de seguridad delante de la puerta trasera
      for (let x = 3; x <= 5; x++) poly([P(x, 14.6), P(x + 1, 14.6), P(x + 1, 15), P(x, 15)], (x & 1) ? '#c9a23a' : '#2a2a2a');
    }
    if (R.id === 'alley') {
      seg(P(1, 18.5), P(8, 18.5), 'rgba(220,210,170,0.25)', 1);
      for (let i = 0; i < 6; i++) { const x = 1.5 + i * 1.1; poly([P(x, 17.4), P(x + 0.3, 17.4), P(x + 0.3, 17.6), P(x, 17.6)], 'rgba(0,0,0,0.25)'); }
    }
    if (R.id === 'vault') {
      // marca del carro
      seg(P(17, 7), P(22, 7), 'rgba(255,200,60,0.35)');
    }
  }
  function doorById(x, y) { const id = E.doorIdAt(x, y); return id ? E.S.doors[id] : null; }
  function doorColors(d) {
    if (d.def.vault) return d.locked ? '#8d939b' : '#3a3f44';
    return d.locked ? '#6b3434' : '#6b5c48';
  }
  function wallTexture(R, a, b, side) {
    const dark = shade(R.wall, side === 'n' ? 0.86 : 0.66);
    if (R.style === 'brick' || R.style === 'block') {
      const step = R.style === 'brick' ? 5 : 8;
      for (let h = step; h < WALL - 2; h += step) seg(up(a, h), up(b, h), dark);
      const n = R.style === 'brick' ? 3 : 2;
      for (let h = 0; h < WALL - step; h += step) {
        const off = ((h / step) & 1) * 0.5 / n;
        for (let i = 0; i < n; i++) {
          const t = off + i / n;
          const m = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
          seg(up(m, h), up(m, h + step), dark);
        }
      }
    } else if (R.style === 'metal') {
      for (let i = 1; i < 3; i++) { const t = i / 3; const m = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]; seg(m, up(m, WALL), dark); }
      seg(up(a, WALL * 0.5), up(b, WALL * 0.5), dark);
    } else {
      seg(up(a, 14), up(b, 14), shade(R.wall, side === 'n' ? 0.9 : 0.7));
    }
    poly([a, b, up(b, 3), up(a, 3)], shade(R.wall, 0.5));
  }
  function wallSeg(R, a, b, door, side) {
    const col = shade(R.wall, side === 'n' ? 1 : 0.78);
    if (!door) {
      poly([a, b, up(b, WALL), up(a, WALL)], col);
      wallTexture(R, a, b, side);
    } else {
      const lerp = t => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
      const a1 = lerp(0.1), b1 = lerp(0.9), DH = door.def.vault ? 32 : 28;
      poly([a, a1, up(a1, WALL), up(a, WALL)], col);
      poly([b1, b, up(b, WALL), up(b1, WALL)], col);
      poly([up(a1, DH), up(b1, DH), up(b1, WALL), up(a1, WALL)], col);
      const open = !door.locked && door.openT > 0;
      poly([a1, b1, up(b1, DH), up(a1, DH)], open ? '#08090b' : doorColors(door));
      if (!open) {
        if (door.def.vault) {
          const c = up(lerp(0.5), DH / 2);
          ctx.strokeStyle = '#5b6067'; ctx.lineWidth = 2;
          ctx.beginPath(); ctx.ellipse(c[0], c[1], 6, 9, 0, 0, Math.PI * 2); ctx.stroke();
          ctx.fillStyle = '#c9ced6'; ctx.fillRect(Math.round(c[0]) - 1, Math.round(c[1]) - 1, 2, 2);
        } else {
          const h = up(lerp(0.75), 14);
          ctx.fillStyle = '#c8b58a'; ctx.fillRect(Math.round(h[0]), Math.round(h[1]), 1, 2);
        }
        const led = up(lerp(0.96), 18);
        ctx.fillStyle = door.locked ? '#ff3b30' : '#3bff8a';
        ctx.fillRect(Math.round(led[0]), Math.round(led[1]), 2, 2);
      }
      seg(up(a1, DH), up(b1, DH), shade(R.wall, 0.45));
    }
    seg(up(a, WALL), up(b, WALL), shade(R.wall, 1.25));
  }
  function drawBackWalls(R) {
    for (let x = R.x; x < R.x + R.w; x++) wallSeg(R, P(x, R.y), P(x + 1, R.y), doorById(x, R.y - 1), 'n');
    for (let y = R.y; y < R.y + R.h; y++) wallSeg(R, P(R.x, y), P(R.x, y + 1), doorById(R.x - 1, y), 'w');
    // cuadros y detalles de pared
    if (R.id === 'hall') {
      const c = up(P(15.5, 9), 26);
      ctx.fillStyle = '#e8e2cf'; ctx.beginPath(); ctx.arc(c[0], c[1], 3, 0, 7); ctx.fill();
      ctx.fillStyle = '#222'; ctx.fillRect(Math.round(c[0]), Math.round(c[1]) - 2, 1, 2);
      faceS(20.3, 20.9, 9, 22, 26, '#1f7a3a');
    }
    if (R.id === 'office') faceS(10.2, 11.4, 12, 20, 30, '#5f7f9a');
    if (R.id === 'security') { faceS(17.4, 18.0, 12, 18, 30, '#c9c3ae'); faceS(17.5, 17.9, 12, 26, 29, '#a33'); }
  }
  function drawFrontDoors(R) {
    const yS = R.y + R.h, xE = R.x + R.w;
    const tile = (d, x, y) => {
      poly([P(x, y), P(x + 1, y), P(x + 1, y + 1), P(x, y + 1)], d.locked ? '#3a2222' : '#2a2a2c');
      if (d.locked) for (let i = 0; i < 3; i++) poly([P(x + 0.15 + i * 0.3, y + 0.4), P(x + 0.3 + i * 0.3, y + 0.4), P(x + 0.3 + i * 0.3, y + 0.6), P(x + 0.15 + i * 0.3, y + 0.6)], '#a33a2c');
    };
    for (let x = R.x; x < xE; x++) { const d = doorById(x, yS); if (d) tile(d, x, yS); }
    for (let y = R.y; y < yS; y++) { const d = doorById(xE, y); if (d) tile(d, xE, y); }
  }
  function drawFrontStubs(R) {
    const yS = R.y + R.h, xE = R.x + R.w, top = shade(R.wall, 1.15), side = shade(R.wall, 0.7);
    for (let x = R.x; x < xE; x++) {
      if (doorById(x, yS)) continue;
      poly([up(P(x, yS), STUB), up(P(x + 1, yS), STUB), up(P(x + 1, yS + 0.22), STUB), up(P(x, yS + 0.22), STUB)], top);
      faceS(x, x + 1, yS + 0.22, 0, STUB, side);
    }
    for (let y = R.y; y < yS + 1; y++) {
      if (doorById(xE, y)) continue;
      const y1 = y === yS ? yS + 0.22 : y + 1;
      poly([up(P(xE, y), STUB), up(P(xE + 0.22, y), STUB), up(P(xE + 0.22, y1), STUB), up(P(xE, y1), STUB)], top);
      faceE(xE + 0.22, y, y1, 0, STUB, shade(R.wall, 0.55));
    }
  }

  // ---------- Mobiliario ----------
  function drawSlice(o, cx, cy) {
    const col = o.col, S = E.S, lit = S.rooms[o.room].lit;
    switch (o.k) {
      case 'crate':
        box(cx + 0.04, cy + 0.04, 0.92, 0.92, o.h, 0, col);
        faceS(cx + 0.04, cx + 0.96, cy + 0.96, o.h * 0.45, o.h * 0.55, shade(col, 0.55));
        faceE(cx + 0.96, cy + 0.04, cy + 0.96, o.h * 0.45, o.h * 0.55, shade(col, 0.45));
        break;
      case 'dumpster':
        box(cx, cy + 0.1, 1, 0.8, o.h, 0, col);
        box(cx, cy + 0.1, 1, 0.8, 2, o.h, '#2a4a39');
        break;
      case 'lamp': {
        box(cx + 0.42, cy + 0.42, 0.16, 0.16, o.h, 0, col);
        box(cx + 0.1, cy + 0.35, 0.45, 0.3, 3, o.h, '#4a4d52');
        const p = up(P(cx + 0.3, cy + 0.5), o.h - 1);
        ctx.fillStyle = '#ffd48a'; ctx.fillRect(Math.round(p[0]) - 2, Math.round(p[1]), 4, 1);
        break;
      }
      case 'shelf': {
        box(cx + 0.05, cy + 0.1, 0.9, 0.8, o.h, 0, col);
        const items = ['#a0603a', '#3a6a8a', '#c9b27a', '#7a3a3a'];
        for (let s = 0; s < 3; s++) {
          const z = 3 + s * 11;
          faceS(cx + 0.05, cx + 0.95, cy + 0.9, z + 8, z + 9, shade(col, 0.5));
          const it = items[(hash(cx, cy + s) >> 2) % items.length];
          faceS(cx + 0.15, cx + 0.5, cy + 0.9, z, z + 7, it);
          faceS(cx + 0.58, cx + 0.85, cy + 0.9, z, z + 5, shade(it, 0.8));
        }
        break;
      }
      case 'pallet':
        box(cx + 0.05, cy + 0.05, 0.9, 0.9, 3, 0, '#7a5c3a');
        box(cx + 0.12, cy + 0.12, 0.76, 0.76, o.h - 3, 3, col);
        break;
      case 'phonetable': {
        box(cx + 0.15, cy + 0.15, 0.7, 0.7, o.h, 0, col);
        box(cx + 0.35, cy + 0.35, 0.3, 0.25, 3, o.h, '#1a1a1a');
        if (S.ringT > 0 && S.ringRoom === o.room && Math.floor(time * 6) % 2) { const p = up(P(cx + 0.5, cy + 0.5), o.h + 6); ctx.fillStyle = '#ffe066'; ctx.fillRect(Math.round(p[0]) - 1, Math.round(p[1]) - 3, 2, 3); }
        break;
      }
      case 'plant': {
        box(cx + 0.3, cy + 0.3, 0.4, 0.4, o.h, 0, col);
        const p = up(P(cx + 0.5, cy + 0.5), o.h + 7);
        ctx.fillStyle = '#35592f'; ctx.beginPath(); ctx.arc(p[0], p[1], 7, 0, 7); ctx.fill();
        ctx.fillStyle = '#4f7f44'; ctx.beginPath(); ctx.arc(p[0] - 2, p[1] - 2, 4, 0, 7); ctx.fill();
        break;
      }
      case 'bench': box(cx + 0.05, cy + 0.3, 0.9, 0.45, o.h, 0, col); break;
      case 'cooler':
        box(cx + 0.25, cy + 0.25, 0.5, 0.5, o.h, 0, col);
        box(cx + 0.32, cy + 0.32, 0.36, 0.36, 10, o.h, '#6aa0d8');
        break;
      case 'desk': {
        box(cx, cy + 0.1, 1, 0.8, o.h, 0, col);
        if (cx === o.x) {
          box(cx + 0.35, cy + 0.25, 0.5, 0.16, 10, o.h, '#2a2d33');
          faceS(cx + 0.38, cx + 0.82, cy + 0.41, o.h + 2, o.h + 9, lit ? '#8fc6d6' : '#2f5f6b');
        } else {
          box(cx + 0.15, cy + 0.3, 0.35, 0.3, 1, o.h, '#e8e4d8');
          if (o.phone) {
            box(cx + 0.58, cy + 0.3, 0.28, 0.25, 3, o.h, '#151515');
            if (S.ringT > 0 && S.ringRoom === o.room && Math.floor(time * 6) % 2) { const p = up(P(cx + 0.72, cy + 0.42), o.h + 9); ctx.fillStyle = '#ffe066'; ctx.fillRect(Math.round(p[0]) - 1, Math.round(p[1]) - 3, 2, 3); }
          }
        }
        break;
      }
      case 'cabinet':
        box(cx + 0.1, cy + 0.1, 0.8, 0.8, o.h, 0, col);
        for (let i = 1; i < 3; i++) faceS(cx + 0.1, cx + 0.9, cy + 0.9, (o.h * i) / 3, (o.h * i) / 3 + 1, shade(col, 0.55));
        break;
      case 'secdesk': {
        box(cx, cy + 0.2, 1, 0.75, o.h, 0, col);
        box(cx + 0.12, cy + 0.25, 0.76, 0.18, 12, o.h, '#22252a');
        const fl = 0.85 + 0.15 * Math.sin(time * 9 + cx);
        faceS(cx + 0.16, cx + 0.84, cy + 0.43, o.h + 2, o.h + 11, shade('#7fd6ff', fl));
        break;
      }
      case 'locker':
        box(cx + 0.1, cy + 0.05, 0.8, 0.9, o.h, 0, col);
        faceS(cx + 0.48, cx + 0.52, cy + 0.95, 2, o.h - 2, shade(col, 0.5));
        break;
      case 'rack': {
        box(cx + 0.05, cy, 0.9, 1, o.h, 0, col);
        for (let i = 0; i < 6; i++) {
          const on = (hash(cx * 7 + i, cy) + Math.floor(time * 3 + i)) % 5 !== 0;
          const yy = cy + 0.15 + (i % 3) * 0.28, z = 10 + Math.floor(i / 3) * 18;
          if (cx < 12) faceE(cx + 0.95, yy, yy + 0.08, z, z + 2, on ? (i % 2 ? '#4cff7a' : '#ffb347') : '#1a1f24');
          else faceS(cx + 0.1 + (i % 3) * 0.25, cx + 0.18 + (i % 3) * 0.25, cy + 1, z, z + 2, on ? '#4cff7a' : '#1a1f24');
        }
        break;
      }
      case 'terminal':
        box(cx + 0.15, cy + 0.2, 0.7, 0.6, o.h, 0, col);
        faceS(cx + 0.22, cx + 0.78, cy + 0.8, o.h - 13, o.h - 3, S.team.code ? '#58e08a' : (Math.floor(time * 2) % 2 ? '#58e08a' : '#2e8a52'));
        break;
      case 'deposit':
        box(cx, cy, 1, 1, o.h, 0, col);
        for (let z = 6; z < o.h - 4; z += 10) {
          if (cy + 1 <= 6) faceS(cx + 0.1, cx + 0.45, cy + 1, z, z + 7, shade(col, 0.72));
          if (cy + 1 <= 6) faceS(cx + 0.55, cx + 0.9, cy + 1, z, z + 7, shade(col, 0.72));
          if (cx === 16) faceE(cx + 1, cy + 0.1, cy + 0.9, z, z + 7, shade(col, 0.56));
        }
        break;
      case 'cart':
        box(cx + 0.1, cy + 0.15, 0.8, 0.7, o.h, 0, col);
        if (!S.team.loot) {
          for (let i = 0; i < 3; i++) box(cx + 0.18 + i * 0.22, cy + 0.25, 0.18, 0.45, 4, o.h, '#e0b040');
          box(cx + 0.28, cy + 0.3, 0.18, 0.35, 4, o.h + 4, '#e8c050');
        }
        break;
    }
  }

  // ---------- Personajes ----------
  const PAL = {
    VITO: { body: '#2b2f36', legs: '#1b1d22', head: '#23262c', skin: '#c49478', hat: '#8f2f2a' },
    NORA: { body: '#2e4a3c', legs: '#1d2420', head: '#23262c', skin: '#d4a588', hat: null, goggles: true },
    'RAMÍREZ': { body: '#3f5d8a', legs: '#1f2a3d', head: '#b98a6c', skin: '#b98a6c', hat: '#1a2233', guard: true },
    CASTRO: { body: '#3a5680', legs: '#1f2a3d', head: '#9c7258', skin: '#9c7258', hat: '#1a2233', guard: true },
  };
  function drawActor(a) {
    const p = P(a.x, a.y), x = Math.round(p[0]), y = Math.round(p[1]);
    const pal = PAL[a.name];
    ctx.fillStyle = 'rgba(0,0,0,0.35)';
    ctx.beginPath(); ctx.ellipse(x, y, 6, 2.5, 0, 0, Math.PI * 2); ctx.fill();
    if (a.downed) {
      ctx.fillStyle = pal.legs; ctx.fillRect(x - 8, y - 3, 6, 3);
      ctx.fillStyle = pal.body; ctx.fillRect(x - 2, y - 4, 6, 4);
      ctx.fillStyle = pal.head; ctx.fillRect(x + 4, y - 4, 3, 3);
      return;
    }
    const sx = a.fx - a.fy, sy = a.fx + a.fy, s = sx < 0 ? -1 : 1, back = sy < -0.25;
    const cr = a.crouch ? 1 : 0, legH = 6 - cr * 2, bodyH = 7 - cr;
    const ph = a.walking ? Math.sin(a.anim * 11) : 0;
    const top = y - legH - bodyH;
    ctx.fillStyle = pal.legs;
    ctx.fillRect(x - 3, y - legH, 2, legH - (ph > 0.3 ? 1 : 0));
    ctx.fillRect(x + 1, y - legH, 2, legH - (ph < -0.3 ? 1 : 0));
    if (a.bag) { ctx.fillStyle = '#4a4030'; ctx.fillRect(s > 0 ? x - 6 : x + 3, top + 1, 3, 6); }
    ctx.fillStyle = pal.body; ctx.fillRect(x - 3, top, 6, bodyH);
    ctx.fillStyle = shade(pal.body, 0.75);
    const sw = Math.round(ph);
    ctx.fillRect(x - 4, top + 1 + sw, 1, 5);
    ctx.fillRect(x + 3, top + 1 - sw, 1, 5);
    ctx.fillStyle = shade(pal.legs, 0.8); ctx.fillRect(x - 3, top + bodyH - 1, 6, 1);
    // cabeza
    ctx.fillStyle = back && pal.guard ? '#2a2018' : pal.head;
    ctx.fillRect(x - 2, top - 4, 4, 4);
    if (!back && !pal.guard) { ctx.fillStyle = pal.skin; ctx.fillRect(x - 1 + (s > 0 ? 0 : -1), top - 3, 3, 1); }
    if (pal.goggles && !back) { ctx.fillStyle = E.S.rooms[a.room].lit ? '#2f6b4a' : '#7dffb0'; ctx.fillRect(x - 1 + (s > 0 ? 0 : -1), top - 3, 3, 1); }
    if (pal.hat) {
      ctx.fillStyle = pal.hat;
      ctx.fillRect(x - 2, top - 5, 4, 2);
      if (pal.guard && !back) ctx.fillRect(s > 0 ? x + 1 : x - 3, top - 4, 2, 1);
    }
    if (pal.guard) {
      ctx.fillStyle = '#e6c84a'; ctx.fillRect(s > 0 ? x : x - 1, top + 2, 1, 1);
      if (!E.S.rooms[a.room].lit) { ctx.fillStyle = '#fff6c0'; ctx.fillRect(s > 0 ? x + 4 : x - 5, top + 4, 2, 1); }
      if (a.state === 'calling' || a.state === 'alarmed') {
        if (Math.floor(time * 6) % 2) { ctx.fillStyle = '#ff4a3a'; ctx.fillRect(x, top - 13, 2, 5); ctx.fillRect(x, top - 7, 2, 1); }
      } else if (a.detect > 0.12) {
        ctx.fillStyle = '#000'; ctx.fillRect(x - 6, top - 10, 12, 3);
        ctx.fillStyle = a.detect > 0.6 ? '#ff6a3a' : '#ffcc55'; ctx.fillRect(x - 5, top - 9, Math.round(10 * Math.min(1, a.detect)), 1);
      }
    }
  }

  // ---------- Luz ----------
  function hole(x, y, r, a) {
    const g = lctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, `rgba(0,0,0,${a})`);
    g.addColorStop(1, 'rgba(0,0,0,0)');
    lctx.fillStyle = g;
    lctx.beginPath(); lctx.arc(x, y, r, 0, Math.PI * 2); lctx.fill();
  }
  function flashlight(g) {
    const ang = Math.atan2(g.fy, g.fx), half = 0.48, R = 3.4;
    const pts = [P(g.x, g.y)];
    for (let i = -4; i <= 4; i++) { const a = ang + (half * i) / 4; pts.push(P(g.x + Math.cos(a) * R, g.y + Math.sin(a) * R)); }
    const c0 = P(g.x, g.y);
    const grd = lctx.createRadialGradient(c0[0], c0[1], 2, c0[0], c0[1], 64);
    grd.addColorStop(0, 'rgba(0,0,0,0.97)');
    grd.addColorStop(1, 'rgba(0,0,0,0)');
    poly(pts, grd, lctx);
    hole(c0[0], c0[1] - 8, 13, 0.75);
  }
  function drawLight(R) {
    const S = E.S, rs = S.rooms[R.id];
    lctx.globalCompositeOperation = 'source-over';
    lctx.clearRect(0, 0, FW, FH);
    let base;
    if (S.ir) base = 0.1;
    else if (R.exterior) base = 0.62;
    else base = rs.lit ? 0.3 : 0.975;
    lctx.fillStyle = `rgba(3,5,10,${base})`;
    lctx.fillRect(0, 0, FW, FH);
    if (S.ir) { ctx.drawImage(lightCv, 0, 0); return; }
    lctx.globalCompositeOperation = 'destination-out';
    if (R.exterior) {
      const p = P(7.4, 18.3); hole(p[0], p[1] - 10, 130, 0.95);
      const d = P(4.5, 15.5); hole(d[0], d[1] - 20, 30, 0.5);
    } else if (rs.lit) {
      for (const l of R.lamps) { const p = P(l.x, l.y); hole(p[0], p[1] - 12, 105, 0.9); }
    } else {
      for (const g of S.guards) if (!g.downed && E.inRoomView(g, R.id)) flashlight(g);
      for (const d of R.doorDefs) {
        const other = d.a === R.id ? d.b : d.a;
        if (S.rooms[other].lit) { const p = P(d.x + 0.5, d.y + 0.5); hole(p[0], p[1] - 4, S.doors[d.id].openT > 0 ? 34 : 14, 0.55); }
      }
      for (const o of E.OBJECTS) {
        if (o.room !== R.id) continue;
        if (o.k === 'secdesk' || o.k === 'terminal' || o.k === 'desk') {
          const p = up(P(o.x + o.w / 2, o.y + o.d), o.h + 4);
          hole(p[0], p[1], o.k === 'desk' ? 10 : 22, 0.55);
        }
        if (o.k === 'rack') { const p = up(P(o.x + 1, o.y + 2), 24); hole(p[0], p[1], 16, 0.35); }
      }
      if (S.ringT > 0 && S.ringRoom === R.id && R.phone) { const p = P(R.phone.x, R.phone.y); hole(p[0], p[1] - 10, 18, 0.5); }
    }
    lctx.globalCompositeOperation = 'source-over';
    ctx.drawImage(lightCv, 0, 0);
    if (R.exterior) { ctx.fillStyle = 'rgba(255,150,60,0.05)'; ctx.fillRect(0, 0, FW, FH); }
  }

  // ---------- Filtro CCTV ----------
  function post(dt) {
    const S = E.S;
    const im = ctx.getImageData(0, 0, FW, FH), d = im.data;
    const off = (Math.random() * NOISE_MASK) | 0;
    const stat = S.staticT > 0;
    const amp = stat ? 1.3 : S.ir ? 0.2 : 0.075;
    const ir = S.ir;
    for (let i = 0, j = 0; i < FW * FH; i++, j += 4) {
      const n = noise[(i + off) & NOISE_MASK] * amp, v = vig[i];
      if (ir) {
        const l = d[j] * 0.35 + d[j + 1] * 0.5 + d[j + 2] * 0.15;
        const g = (l * 1.45 + 14) * v + n;
        d[j] = g * 0.36; d[j + 1] = g; d[j + 2] = g * 0.5;
      } else if (stat) {
        const g = 90 + n;
        d[j] = g; d[j + 1] = g; d[j + 2] = g;
      } else {
        d[j] = d[j] * v + n; d[j + 1] = d[j + 1] * v + n; d[j + 2] = d[j + 2] * v + n * 1.1;
      }
    }
    ctx.putImageData(im, 0, 0);
    // banda de interferencia que baja despacio
    const by = Math.floor((time * 18) % (FH + 40)) - 20;
    ctx.fillStyle = ir ? 'rgba(120,255,140,0.05)' : 'rgba(255,255,255,0.035)';
    ctx.fillRect(0, by, FW, 6);
  }

  function fx(R) {
    for (const f of E.S.fx) {
      if (f.room !== R.id) continue;
      const p = up(P(f.x, f.y), 10);
      ctx.strokeStyle = `rgba(255,255,255,${Math.min(1, f.t * 2)})`;
      ctx.lineWidth = 1;
      for (let i = 0; i < 6; i++) {
        const a = (i / 6) * Math.PI * 2, r0 = 4, r1 = 4 + (0.5 - f.t) * 24;
        ctx.beginPath(); ctx.moveTo(p[0] + Math.cos(a) * r0, p[1] + Math.sin(a) * r0); ctx.lineTo(p[0] + Math.cos(a) * r1, p[1] + Math.sin(a) * r1); ctx.stroke();
      }
    }
  }

  function draw(dt) {
    const S = E.S;
    time += dt;
    S.staticT = Math.max(0, S.staticT - dt);
    const R = E.ROOM[S.view];
    setView(R);
    ctx.fillStyle = '#050607';
    ctx.fillRect(0, 0, FW, FH);
    drawFloor(R);
    drawBackWalls(R);
    drawFrontDoors(R);
    const list = [];
    for (const o of E.OBJECTS) {
      if (o.room !== R.id) continue;
      for (let y = o.y; y < o.y + o.d; y++) for (let x = o.x; x < o.x + o.w; x++) list.push({ k: x + y + 1, o, x, y });
    }
    for (const a of S.actors) if (E.inRoomView(a, R.id)) list.push({ k: a.x + a.y + 0.02, a });
    list.sort((p, q) => p.k - q.k);
    for (const it of list) { if (it.a) drawActor(it.a); else drawSlice(it.o, it.x, it.y); }
    drawFrontStubs(R);
    fx(R);
    drawLight(R);
    post(dt);
  }

  E.render = { init, draw };
})();
