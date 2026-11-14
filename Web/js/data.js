'use strict';
// Datos fijos de la demo: edificio, puertas, mobiliario y frases de radio.
(function () {
  const E = (window.EYE = window.EYE || {});

  E.CONST = {
    FW: 384, FH: 216, HW: 16, HH: 8, WALL: 40,
    CLOCK_START: 2 * 3600 + 15 * 60,
    CLOCK_RATE: 3,
    MAP_W: 24, MAP_H: 20,
    ROUND_AT: 300,
  };

  // Ajustes por dificultad. Se elige en la portada.
  E.TUNES = {
    normal: { attempts: 3, slowmo: 0.3, puzzleTime: 1, suspicion: 1, lockout: 12 },
    relaxed: { attempts: 4, slowmo: 0.18, puzzleTime: 1.5, suspicion: 0.65, lockout: 6 },
  };
  E.TUNE = E.TUNES.normal;

  E.ROOMS = [
    { id: 'alley', name: 'CALLEJÓN', cam: 1, x: 1, y: 16, w: 7, h: 3, exterior: true,
      inP: 'en el callejón', ofP: 'del callejón', floor: ['#3a3c41', '#36383d'], wall: '#6a4a3d', style: 'brick' },
    { id: 'dock', name: 'MUELLE', cam: 2, x: 1, y: 9, w: 7, h: 6, phone: { x: 2, y: 14 },
      inP: 'en el muelle', ofP: 'del muelle', floor: ['#6b685f', '#66635a'], wall: '#7f7b70', style: 'block' },
    { id: 'hall', name: 'PASILLO', cam: 3, x: 9, y: 9, w: 14, h: 2, watched: true,
      inP: 'en el pasillo', ofP: 'del pasillo', floor: ['#8d8775', '#878170'], wall: '#a39c86', style: 'plaster' },
    { id: 'office', name: 'OFICINAS', cam: 4, x: 9, y: 12, w: 7, h: 6, watched: true, phone: { x: 13, y: 14 },
      inP: 'en las oficinas', ofP: 'de las oficinas', floor: ['#4f5a68', '#4b5664'], wall: '#b0a993', style: 'plaster' },
    { id: 'security', name: 'SEGURIDAD', cam: 5, x: 17, y: 12, w: 6, h: 4,
      inP: 'en seguridad', ofP: 'de seguridad', floor: ['#5b5f58', '#575b54'], wall: '#8e9483', style: 'plaster' },
    { id: 'servers', name: 'SERVIDORES', cam: 6, x: 9, y: 2, w: 6, h: 6,
      inP: 'en servidores', ofP: 'de servidores', floor: ['#3d424a', '#393e46'], wall: '#5a616c', style: 'metal' },
    { id: 'vault', name: 'BÓVEDA', cam: 7, x: 16, y: 2, w: 7, h: 6, watched: true,
      inP: 'en la bóveda', ofP: 'de la bóveda', floor: ['#55585d', '#515459'], wall: '#6f747a', style: 'metal' },
  ];

  E.DOORS = [
    { id: 'back', name: 'Puerta trasera', x: 4, y: 15, a: 'alley', b: 'dock', locked: true },
    { id: 'dockhall', name: 'Puerta del muelle', x: 8, y: 10, a: 'dock', b: 'hall' },
    { id: 'office', name: 'Puerta de oficinas', x: 12, y: 11, a: 'hall', b: 'office' },
    { id: 'security', name: 'Puerta de seguridad', x: 18, y: 11, a: 'hall', b: 'security' },
    { id: 'servers', name: 'Puerta de servidores', x: 11, y: 8, a: 'hall', b: 'servers', locked: true },
    { id: 'vault', name: 'Puerta de la bóveda', x: 19, y: 8, a: 'hall', b: 'vault', locked: true, vault: true },
  ];

  E.OBJECTS = [
    // Callejon
    { k: 'dumpster', x: 6, y: 16, w: 2, h: 20, col: '#36604a' },
    { k: 'lamp', x: 7, y: 18, h: 60, col: '#3a3d42' },
    { k: 'crate', x: 1, y: 16, h: 14, col: '#7d6040' },
    // Muelle
    { k: 'crate', x: 1, y: 9, w: 2, d: 2, h: 24, col: '#8a6a42' },
    { k: 'shelf', x: 4, y: 9, w: 3, h: 36, col: '#4f565e' },
    { k: 'pallet', x: 2, y: 13, h: 12, col: '#a8a294' },
    { k: 'crate', x: 6, y: 13, w: 2, h: 14, col: '#7d6040' },
    { k: 'phonetable', x: 1, y: 14, h: 12, col: '#5b4b3a' },
    // Pasillo
    { k: 'plant', x: 9, y: 9, h: 10, col: '#7a5a3e' },
    { k: 'bench', x: 14, y: 9, w: 2, h: 8, col: '#6b6f75' },
    { k: 'cooler', x: 22, y: 9, h: 24, col: '#d5dadf' },
    // Oficinas
    { k: 'desk', x: 10, y: 13, w: 2, h: 14, col: '#8a6d4b' },
    { k: 'desk', x: 13, y: 13, w: 2, h: 14, col: '#8a6d4b', phone: true },
    { k: 'desk', x: 10, y: 15, w: 2, h: 14, col: '#8a6d4b' },
    { k: 'desk', x: 13, y: 15, w: 2, h: 14, col: '#8a6d4b' },
    { k: 'cabinet', x: 15, y: 12, h: 26, col: '#7c8288' },
    { k: 'plant', x: 9, y: 17, h: 10, col: '#7a5a3e' },
    // Seguridad
    { k: 'secdesk', x: 19, y: 12, w: 3, h: 14, col: '#4b5058' },
    { k: 'locker', x: 22, y: 14, d: 2, h: 36, col: '#5f6f7d' },
    // Servidores
    { k: 'rack', x: 9, y: 2, d: 4, h: 40, col: '#2b3038' },
    { k: 'rack', x: 14, y: 2, d: 4, h: 40, col: '#2b3038' },
    { k: 'terminal', x: 11, y: 2, h: 18, col: '#3a4048' },
    // Boveda
    { k: 'deposit', x: 16, y: 2, w: 7, h: 46, col: '#7d8288' },
    { k: 'deposit', x: 16, y: 3, d: 3, h: 46, col: '#7d8288' },
    { k: 'deposit', x: 22, y: 3, d: 3, h: 46, col: '#7d8288' },
    { k: 'cart', x: 19, y: 4, h: 10, col: '#5a5e63' },
  ];

  // Frases en secuencia: [canal, quien, texto]. Canales: crew, security, police.
  E.LINES = {
    crew_start: [
      ['crew', 'VITO', 'Aquí Vito. Estamos en el callejón, listos para entrar.'],
      ['crew', 'NORA', 'Nosotros nos movemos solos. Tú ábrenos camino y vigila las cámaras.'],
    ],
    crew_need_door: [['crew', 'VITO', '{door} cerrada. Ábrela cuando puedas.']],
    crew_need_door_ahead: [['crew', 'NORA', 'Ve abriendo la {door}, no quiero esperar a la vista.']],
    crew_door_open: [['crew', 'NORA', 'Abierta. Seguimos.']],
    crew_need_loop: [['crew', 'NORA', 'Hay cámara {in} y Castro la está mirando. Ponla en bucle o apaga la luz.']],
    crew_cam_seen: [['crew', 'NORA', '¡La cámara {in} nos está enfocando!']],
    crew_go_anyway: [['crew', 'VITO', 'No podemos esperar más. Entramos igual.']],
    crew_guard_ahead: [['crew', 'VITO', 'Guardia {in}. Esperamos a que se mueva.']],
    crew_guard_stays: [['crew', 'VITO', 'No se mueve. Haz sonar un teléfono, o apaga la luz y me encargo yo.']],
    crew_guard_near: [['crew', 'NORA', '¡Viene alguien! Nos quedamos quietos.']],
    crew_dark: [['crew', 'VITO', 'Gafas puestas. A oscuras mandamos nosotros.']],
    crew_hunt: [['crew', 'VITO', 'Lo veo en la oscuridad. Voy a por él.']],
    crew_spotted: [['crew', 'NORA', '¡Nos ha visto! ¡Vito, que no hable!']],
    crew_cut_call: [['crew', 'VITO', 'Le corté la radio a media frase.']],
    crew_call_done: [['crew', 'NORA', 'Ha avisado por radio. Esto se complica.']],
    crew_terminal: [['crew', 'NORA', 'Estoy en el terminal. Sácame el código de la bóveda.']],
    crew_code: [['crew', 'NORA', 'Tengo el código. Vamos a la bóveda.']],
    crew_vault: [['crew', 'VITO', 'Delante de la bóveda. Toda tuya, hacker.']],
    crew_vault_open: [['crew', 'VITO', '¡Abierta! Cargamos y nos largamos.']],
    crew_loot: [['crew', 'NORA', 'Bolsas llenas. Salimos por donde entramos.']],
    crew_escape: [['crew', 'VITO', 'Estamos fuera. Arranca, nos vamos.']],
    crew_hack_fail: [['crew', 'NORA', 'Eso ha hecho ruido en algún sitio...']],
    crew_spoof: [['crew', 'NORA', 'Están pidiendo un control por radio. Contesta tú, tienes la voz grabada.']],
    crew_police_close: [['crew', 'VITO', 'He oído el escáner. Hay que darse prisa.']],

    sec_checkin: [['security', 'CASTRO', 'Ramírez, aquí Castro. ¿Todo en orden?']],
    sec_checkin_ok: [['security', 'RAMÍREZ', 'Todo tranquilo, jefe.']],
    sec_checkin_none: [['security', 'CASTRO', '¿Ramírez? Ramírez, responde.']],
    sec_checkin_lost: [['security', 'CASTRO', 'Ramírez no contesta. Voy a buscarle.']],
    sec_central_check: [['security', 'CENTRAL SEG.', 'Puesto Industrial 14, control horario. ¿Sin novedad?']],
    sec_central_spoof: [['security', 'CASTRO', 'Sin novedad, central.']],
    sec_central_none: [['security', 'CENTRAL SEG.', 'Industrial 14 no responde. Aviso a la policía.']],
    sec_spoof_ok: [['security', 'RAMÍREZ', 'Todo tranquilo, jefe.']],
    sec_spoof_bad: [['security', 'CASTRO', 'Esa no era la voz de Ramírez... Voy a mirar.']],
    sec_body: [['security', 'CASTRO', '¡Central, Ramírez está en el suelo! ¡Hay alguien dentro!']],
    sec_cam: [['security', 'CASTRO', 'He visto algo en la cámara {of}. Voy a mirar.']],
    sec_lights: [['security', 'CASTRO', 'Se ha ido la luz {in}. Qué raro.']],
    sec_phone: [['security', '{who}', '¿Diga? ... ¿Hola? Nada, han colgado.']],
    sec_intruders: [['security', '{who}', '¡Central, intrusos {in}!']],
    sec_round: [['security', 'CASTRO', 'Me doy una vuelta por abajo. Ramírez, ojo al pasillo.']],
    sec_round_back: [['security', 'CASTRO', 'Todo en orden. Vuelvo a los monitores.']],
    sec_nothing: [['security', 'CASTRO', 'Aquí no hay nada. Vuelvo al puesto.']],

    pol_round_notice: [['police', 'CENTRAL', 'Unidad 7, recuerde su ronda por el polígono industrial a las dos y media.']],
    pol_rumor: [['police', 'CENTRAL', 'Atención: aviso sin confirmar de actividad en el polígono industrial.']],
    pol_outage: [['police', 'CENTRAL', 'La eléctrica reporta cortes de luz en el polígono.']],
    pol_verify: [
      ['police', 'CENTRAL', 'Unidad 7, acérquese a Industrial 14 a echar un vistazo.'],
      ['police', 'UNIDAD 7', 'Recibido, central. Voy para allá.'],
    ],
    pol_eta_far: [['police', 'UNIDAD 7', 'Unidad 7, a unos minutos del polígono.']],
    pol_eta_near: [['police', 'UNIDAD 7', 'Unidad 7 entrando en el polígono.']],
    pol_enroute: [
      ['police', 'CENTRAL', 'Unidad 14: posible robo en Industrial 14. Prioridad uno.'],
      ['police', 'UNIDAD 14', 'En camino, con sirena.'],
    ],
    pol_eta14: [['police', 'UNIDAD 14', 'Unidad 14, a un minuto.']],
    pol_arrived: [['police', '{unit}', 'Unidad en la puerta de Industrial 14. Entramos.']],
    pol_round_pass: [['police', 'UNIDAD 7', 'Unidad 7 pasando por el polígono. Todo tranquilo.']],
    pol_round_odd: [['police', 'UNIDAD 7', 'Unidad 7 en el polígono. Industrial 14 está a oscuras, lo anoto.']],
  };

  // Frases con variantes: se elige una al azar.
  E.VARIANTS = {
    crew_takedown: [
      ['crew', 'VITO', 'Fuera de juego.'],
      ['crew', 'VITO', 'Dormido. Seguimos.'],
      ['crew', 'NORA', 'Uno menos.'],
    ],
    pol_chatter: [
      ['police', 'CENTRAL', 'Unidad 3, accidente leve en la avenida norte. Sin heridos.'],
      ['police', 'UNIDAD 9', 'Central, el ruido vecinal de la calle Olmo está resuelto.'],
      ['police', 'CENTRAL', 'Unidades, buscamos un coche gris con matrícula parcial cuatro, cuatro, uno.'],
      ['police', 'UNIDAD 3', 'Control de alcoholemia en la rotonda. Sin novedad.'],
      ['police', 'CENTRAL', 'Unidad 12, la alarma del comercio del centro era falsa.'],
    ],
  };
})();
