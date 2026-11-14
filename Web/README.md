# The Eye in the Sky — demo web

Una versión jugable en el navegador, más sencilla de jugar que el MVP de Unity. No necesita instalar nada: es HTML, CSS y JavaScript sin dependencias.

## Cómo abrirla

```bash
node Web/serve.js
```

Después abre <http://localhost:8642>. También funciona abriendo `Web/index.html` directamente con doble clic.

## Qué cambia respecto al MVP de Unity

| | MVP de Unity | Demo web |
|---|---|---|
| **Banda** | Recibe órdenes del jugador | **Va sola.** Elige el camino, espera si hay peligro y noquea guardias cuando la sala está a oscuras o el guardia se acerca demasiado. |
| **Papel del jugador** | Dirigir y hackear | **Responder a peticiones.** La banda pide ayuda en un panel (abrir una puerta, poner una cámara en bucle, apartar a un guardia o contestar un control por radio) y cada petición trae su botón. |
| **Hackeos** | Largos; la imagen se queda ciega | **Cortos, de 5 a 20 segundos.** El edificio va a cámara lenta mientras hackeas y la imagen sigue visible. `ESC` abandona el hackeo sin penalización. |
| **Puzzles** | Siete, varios exigentes | **Seis versiones simplificadas:** SINCRONÍA (puertas), BUCLE (cámaras), CÓDIGO (terminal), HUELLA y CORTAFUEGOS (bóveda) y PARCHE (suplantar una voz). |
| **Cámaras** | Se cambian a mano | **Siguen a la banda** de forma automática; se puede desactivar con SEGUIR BANDA. |
| **Dificultad** | Única | **Normal o relajada**, que da más tiempo, más intentos y menos sospecha. |

Se mantiene lo esencial del spec:

- **Policía:** nunca aparece en pantalla y solo se sabe de ella por el escáner.
- **Oscuridad:** ciega de verdad. Sin infrarrojos (`I`) no se ve nada.
- **Teléfonos:** al sonar, atraen al guardia más cercano.
- **Guardias noqueados:** provocan controles por radio que hay que suplantar.
- **Informe final:** explica qué hizo salir a la policía.

## Archivos

| Archivo | Contenido |
|---|---|
| `js/data.js` | Edificio, puertas, mobiliario, frases de radio y ajustes de dificultad |
| `js/sim.js` | Banda autónoma, guardias, cámaras, controles por radio y policía oculta |
| `js/render.js` | Imagen isométrica en pixel art, luz y oscuridad, infrarrojos y filtro CCTV |
| `js/puzzles.js` | Los seis hackeos y la sesión de hackeo (intentos, tiempo, bloqueo) |
| `js/audio.js` | Sonido sintetizado y voces del navegador |
| `js/ui.js` | Consola: peticiones, acciones por sala, plano, radio, portada e informe |

`EYE.debug` en la consola del navegador permite avanzar el tiempo (`EYE.debug.skip(30)`) o resolver el hackeo abierto (`EYE.debug.solve()`).
