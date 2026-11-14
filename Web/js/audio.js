'use strict';
// Sonido sintetizado (zumbido de la furgoneta, radio, sirena) y voces del navegador.
(function () {
  const E = window.EYE;
  let ac = null, master = null, sirenG = null, sirenO = null, sirenT = 0, sirenHi = false;
  let voices = [], esVoice = null, pending = 0;
  const A = (E.audio = { voice: true });

  function noiseBuf(sec, brown) {
    const n = Math.floor(ac.sampleRate * sec), b = ac.createBuffer(1, n, ac.sampleRate), d = b.getChannelData(0);
    let last = 0;
    for (let i = 0; i < n; i++) {
      const w = Math.random() * 2 - 1;
      if (brown) { last = (last + 0.02 * w) / 1.02; d[i] = last * 3.5; } else d[i] = w;
    }
    return b;
  }
  let whiteBuf = null;

  A.init = function () {
    if (ac) { if (ac.state === 'suspended') ac.resume(); return; }
    try { ac = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { ac = null; return; }
    master = ac.createGain(); master.gain.value = 0.8; master.connect(ac.destination);
    whiteBuf = noiseBuf(1, false);
    const src = ac.createBufferSource(); src.buffer = noiseBuf(4, true); src.loop = true;
    const lp = ac.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 220;
    const g = ac.createGain(); g.gain.value = 0.2;
    src.connect(lp).connect(g).connect(master); src.start();
    const o = ac.createOscillator(); o.frequency.value = 52;
    const og = ac.createGain(); og.gain.value = 0.012;
    o.connect(og).connect(master); o.start();
    sirenO = ac.createOscillator(); sirenO.type = 'sawtooth'; sirenO.frequency.value = 650;
    const sf = ac.createBiquadFilter(); sf.type = 'lowpass'; sf.frequency.value = 1500;
    sirenG = ac.createGain(); sirenG.gain.value = 0;
    sirenO.connect(sf).connect(sirenG).connect(master); sirenO.start();
    loadVoices();
  };

  A.blip = (f, dur, type, vol) => {
    if (!ac) return;
    dur = dur || 0.06;
    const t = ac.currentTime, o = ac.createOscillator(), g = ac.createGain();
    o.type = type || 'square'; o.frequency.value = f || 880;
    g.gain.setValueAtTime(vol || 0.05, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(master); o.start(t); o.stop(t + dur + 0.02);
  };
  function burst(freq, dur, vol) {
    if (!ac) return;
    const t = ac.currentTime, s = ac.createBufferSource(), bp = ac.createBiquadFilter(), g = ac.createGain();
    s.buffer = whiteBuf; bp.type = 'bandpass'; bp.frequency.value = freq; bp.Q.value = 0.8;
    g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(bp).connect(g).connect(master); s.start(t); s.stop(t + dur + 0.02);
  }
  A.squelch = ch => burst(ch === 'police' ? 2600 : ch === 'security' ? 2000 : 1500, 0.12, 0.06);
  A.ok = () => { A.blip(660, 0.08); setTimeout(() => A.blip(990, 0.1), 80); setTimeout(() => A.blip(1320, 0.14), 160); };
  A.bad = () => A.blip(140, 0.3, 'sawtooth', 0.08);
  A.tick = () => A.blip(1400, 0.03, 'square', 0.03);
  A.click = () => burst(4000, 0.05, 0.05);
  A.ping = () => { A.blip(1046, 0.07, 'sine', 0.06); setTimeout(() => A.blip(1318, 0.09, 'sine', 0.05), 70); };
  A.alert = () => { A.blip(880, 0.12, 'square', 0.06); setTimeout(() => A.blip(660, 0.18, 'square', 0.06), 120); };
  A.clunk = () => { A.blip(90, 0.12, 'square', 0.08); burst(300, 0.1, 0.08); };
  A.thud = () => {
    if (!ac) return;
    const t = ac.currentTime, o = ac.createOscillator(), g = ac.createGain();
    o.frequency.setValueAtTime(130, t); o.frequency.exponentialRampToValueAtTime(40, t + 0.22);
    g.gain.setValueAtTime(0.22, t); g.gain.exponentialRampToValueAtTime(0.0001, t + 0.25);
    o.connect(g).connect(master); o.start(t); o.stop(t + 0.3);
  };
  A.ring = () => { for (let i = 0; i < 3; i++) setTimeout(() => { A.blip(440, 0.2, 'sine', 0.04); A.blip(480, 0.2, 'sine', 0.04); }, i * 300); };

  A.update = () => {
    if (!ac) return;
    const S = E.S, P = S.police;
    let lvl = 0;
    if (P.state === 3 && P.eta < 20) lvl = 1 - Math.max(0, P.eta) / 20;
    if (S.over === 'lose') lvl = 1;
    sirenG.gain.setTargetAtTime(lvl * 0.06, ac.currentTime, 0.1);
    sirenT += 1 / 60;
    if (sirenT > 0.45) { sirenT = 0; sirenHi = !sirenHi; sirenO.frequency.setTargetAtTime(sirenHi ? 920 : 640, ac.currentTime, 0.05); }
  };

  // ---------- Voces ----------
  function loadVoices() {
    if (!window.speechSynthesis) return;
    voices = speechSynthesis.getVoices();
    esVoice = voices.find(v => /^es[-_]ES/i.test(v.lang) && /natural|online/i.test(v.name))
      || voices.find(v => /^es[-_]ES/i.test(v.lang))
      || voices.find(v => /^es/i.test(v.lang)) || null;
  }
  if (window.speechSynthesis) speechSynthesis.onvoiceschanged = loadVoices;
  const SPK = {
    VITO: [0.7, 1.05], NORA: [1.3, 1.1], CASTRO: [0.6, 1.0], 'RAMÍREZ': [0.95, 1.0],
    CENTRAL: [1.15, 1.12], 'CENTRAL SEG.': [1.05, 1.12], 'UNIDAD 7': [0.85, 1.05], 'UNIDAD 14': [1.0, 1.1],
  };
  A.speak = function (who, text, ch) {
    if (!A.voice || !window.speechSynthesis || !ac) return;
    if (pending > 3 && ch !== 'crew') return;
    if (pending > 5) return;
    const u = new SpeechSynthesisUtterance(text);
    u.lang = 'es-ES';
    if (esVoice) u.voice = esVoice;
    const s = SPK[who] || [1, 1.05];
    u.pitch = s[0]; u.rate = s[1];
    u.volume = ch === 'crew' ? 1 : 0.8;
    u.onstart = () => A.squelch(ch);
    u.onend = u.onerror = () => { pending = Math.max(0, pending - 1); A.squelch(ch); };
    pending++;
    speechSynthesis.speak(u);
  };
  A.setVoice = on => { A.voice = on; if (!on && window.speechSynthesis) { speechSynthesis.cancel(); pending = 0; } };
})();
