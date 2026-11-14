# THE EYE IN THE SKY — Spec del MVP

**Estado:** MVP implementado (M0 a M5), pendiente de validarlo jugando.
**Objetivo:** prueba de concepto jugable para presentar la idea. Un solo golpe, un solo edificio, de 10 a 15 minutos.

Los números son valores iniciales. Todos irán en ScriptableObjects para afinarlos sin recompilar.

---

## 1. El concepto

Eres un hacker metido en una camioneta aparcada frente a un edificio. Nunca te vemos. Solo existen tus monitores, tu radio y el escáner de la policía.

Una banda de ladrones (NPC) está robando el edificio. No los controlas: **los diriges**. Les dices por dónde ir, cuándo parar, y les abres el camino hackeando cámaras, luces, puertas y sensores.

**Pierdes si llega la policía con la banda dentro.** Y nada en pantalla te dice cuándo va a llegar: solo lo puedes saber escuchando.

### Los tres pilares

1. **Solo tienes un par de ojos.** Ves una cámara a la vez. Mientras hackeas, no ves ninguna.
2. **La amenaza se escucha, no se lee.** La policía no tiene barra, ni icono, ni cuenta atrás. Tiene una frecuencia de radio.
3. **Cada ayuda tiene un precio.** Apagar una luz ayuda a la banda y te deja ciego a ti. Noquear a un guardia abre el paso y deja un control de radio sin responder.

### Referencias

Son referencias de sensación, no de contenido. El juego tiene su propio mundo, personajes y arte.

- Consola de apoyo que asiste a un equipo desde lejos y puede hacerlo fracasar.
- Cambiar de cámara con un mapa, con estática y sin poder mirarlo todo.
- Puzzles de hackeo con tiempo límite e intentos contados.
- Isométrico legible de supervivencia con baldosas.

---

## 2. Reglas de inmersión (no negociables)

- **Prohibido cualquier indicador de la policía:** barras, estrellas, porcentajes, cuentas atrás, iconos, avisos en pantalla.
- La música no reacciona al estado de la policía. Si la hay, no delata nada.
- Lo que sí se permite, porque existe dentro del mundo:
  - el escáner de radio,
  - la hora real en la imagen de las cámaras (`02:14:33`, es un reloj, no una cuenta atrás),
  - una nota de papel fija con las calles del barrio (ver 6.3),
  - la cámara exterior, las luces azules y rojas y las sirenas lejanas cuando ya estén ahí.
- **El mapa no muestra personas.** Ni a la banda ni a los guardias. Si no los ves por una cámara o no te lo dicen por radio, no sabes dónde están.
- **La oscuridad es real** (ver 5). Nunca hay contornos, iconos ni ayudas sobre lo que está a oscuras.
- Nunca vemos al hacker, ni sus manos, ni el interior de la camioneta.
- Solo al terminar, la pantalla de informe revela la línea de tiempo oculta.

---

## 3. La pantalla

Una sola pantalla fija, sin moverse del asiento.

```
┌───────────────────────────────────────────────┬──────────────────┐
│ CAM 04 · GALERÍA ESTE        02:14:33   ● REC │  MAPA            │
│                                               │  cámaras, puertas│
│                                               │  y luces         │
│              IMAGEN DE LA CÁMARA              │  (sin personas)  │
│              (isométrica, con filtro)         ├──────────────────┤
│                                               │  BANDA           │
│                                               │  órdenes         │
├───────────────────────────────────────────────┴──────────────────┤
│  RADIO: canal banda (siempre)  |  ESCÁNER: POLICÍA ◄► SEGURIDAD  │
└──────────────────────────────────────────────────────────────────┘
```

- **Cambiar de cámara:** clic en el mapa o teclas 1–9. Ráfaga de estática de ~0,25 s.
- **Solo una imagen a la vez.** Las demás siguen pasando sin que las veas.
- **Durante un hackeo** el puzzle ocupa el monitor principal. No ves ninguna cámara, solo oyes la radio.
- Se juega con ratón; las teclas son atajos. CORTAFUEGOS y PARCHE se juegan con teclado.

---

## 4. Dirigir a la banda

### 4.1 La banda

| | **Liebre** | **Yunque** |
|---|---|---|
| Función | Sigilosa, abre cerraduras, carga el botín | Fuerza, neutraliza guardias |
| Ruido | Bajo | Medio |
| Cerradura mecánica | 6 s | No puede |
| Noquear | No | Sí |
| Si la descubren | Huye y se esconde | Se enfrenta si está cerca |

Tú eres **Ojo** para ellos. Hablan contigo por radio: confirman, dudan, se asustan y avisan de lo que oyen.

### 4.2 Órdenes

Se elige a uno de los dos y se da la orden en el mapa o sobre la imagen.

| Orden | Efecto |
|---|---|
| **Ir a** | Sale hacia una sala o puerta. Se pueden encadenar puntos para marcar la ruta. |
| **Alto** | Se detiene ya, donde esté. |
| **Esconderse** | Va al escondite más cercano. |
| **Noquear** (Yunque) | Va a por el guardia señalado. Silencioso. El guardia despierta a los ~4 min. |
| **Eliminar** (Yunque) | Definitivo, pero el disparo se oye. Puede provocar una llamada de un vecino. |
| **Retirada** | Vuelven a la camioneta con lo que lleven. |

Para señalar a un guardia hay que verlo en la imagen.

### 4.3 Lo que hacen sin ti

- Responden por radio antes de moverse (0,5–1,5 s). Esa demora importa.
- Sin órdenes, esperan y te lo piden: *"Ojo, ¿por dónde?"*.
- Si oyen pasos cerca y no les dices nada, se esconden solos unos segundos después. No son suicidas.
- Ninguno inventa rutas a través de una puerta cerrada ni resuelve un hackeo.
- **Noquear de frente con luz falla** y el guardia da el aviso. Hay que llegar por detrás, o a oscuras: con las luces apagadas funciona desde cualquier ángulo. La banda lleva visión nocturna.

---

## 5. La oscuridad y las luces

Cada sala tiene un estado de luz (encendida o apagada) que hackeas. Ese mismo dato lo leen **la imagen y la simulación**, así que no puede ser solo estético.

**Efecto en una sala a oscuras:**

| Quién | Efecto |
|---|---|
| **Jugador** | La imagen queda prácticamente negra. No se distinguen personas ni muebles. Solo se ve lo que emite luz: linternas, carteles de salida, disparos. |
| **Guardias** | Encienden la linterna y solo ven dentro de su haz, con un 40 % del alcance. |
| **Banda** | Se mueve bien y puede neutralizar desde cualquier ángulo. |

**Precios de apagar una luz:**

- Te quedas ciego en esa sala. Dependes de lo que te digan por radio.
- Un apagón pone a los guardias en alerta. Uno va al cuarto eléctrico y la restaura (45–90 s).
- Suma sospecha a la policía.
- Se puede volver a encender desde el mapa, a ciegas.

**Cámaras IR:** 2 de las 8 pueden pasar a infrarrojo. Imagen monocroma y ruidosa, con siluetas de personas a corta distancia y sin detalle de la sala. Es la única forma de ver dentro de lo oscuro, y solo hay en dos lugares.

---

## 6. La policía (oculta)

Sistema interno que el jugador jamás ve como dato.

### 6.1 Estados

| Estado | Condición | Qué se oye | Tiempo hasta llegar |
|---|---|---|---|
| **Calma** | Sospecha < 25 | Charla rutinaria de otros incidentes | — |
| **Rumor** | ≥ 25 | La central menciona la zona de pasada | — |
| **Verificación** | ≥ 50 | Se asigna una unidad sin prisa | 180–240 s |
| **En camino** | ≥ 80 o alarma confirmada | Unidad con prioridad, calles cada vez más cerca | 90–120 s |
| **Llegada** | Tiempo a cero | *"Estamos en la puerta."* | Fin |

El tiempo restante solo puede bajar al escalar, nunca subir, salvo por INTERFERENCIA (8.4).
La sospecha baja muy despacio en Calma y Rumor. A partir de Verificación ya no baja.

### 6.2 Qué la provoca

| Hecho | Efecto |
|---|---|
| Intento de hackeo fallido | +8 |
| Hackeo agotado (bloqueo del sistema) | +25 |
| Apagón | +10, los siguientes +20 |
| Guardia informa de algo raro | +5 |
| Control de radio sin responder | +20 |
| Disparo | +35 |
| Cuerpo encontrado | +60 |
| Sensor o alarma silenciosa | Directo a En camino |
| Guardia ve a la banda y termina su aviso | Directo a En camino |

**Ventana de aviso:** el guardia que ve a la banda tarda ~3 s en avisar por radio. Si lo neutralizan antes, no llega el aviso.

**Ronda programada:** para que el golpe no pueda durar para siempre, hacia las 02:30 (±2 min) una patrulla de ronda activa el estado Verificación aunque no hayas cometido ningún error. La radio lo menciona al principio, y la hora de las cámaras es la única referencia. El golpe empieza a las 02:15.

### 6.3 Cómo se siente

- La central nombra calles reales del barrio ficticio. En la pantalla hay una **nota de papel estática** con esas calles dibujadas alrededor del edificio. No marca nada: hay que saber escuchar para usarla.
- Las sirenas se oyen muy lejanas en los últimos ~20 s, desde fuera de la camioneta.
- A veces la central ordena **acercarse sin sirenas**. Quien no estaba escuchando el escáner no se entera.
- Al llegar: luces azules y rojas en la cámara exterior, *"Estamos en la puerta"*, las cámaras se cortan una a una.

---

## 7. La radio

| Canal | Cómo funciona |
|---|---|
| **Banda** | Siempre abierto. Tiene prioridad. |
| **Escáner: POLICÍA** | Se elige con un dial. |
| **Escáner: SEGURIDAD** | La radio interna de los guardias: controles, rondas, *"voy a mirar el cuadro eléctrico"*. |

- **El escáner solo sintoniza una frecuencia a la vez.** Otro punto donde no puedes atenderlo todo.
- Filtro de radio: paso de banda, distorsión, estática, clic de inicio y final.
- **Transcripción:** solo mientras suena la frase, solo sus palabras, sin historial y desactivable. La frase nunca se resume ni se interpreta para el jugador.
- Volumen aproximado de líneas: 40 de policía, 20 de seguridad, 60 de banda.

**Voces en el MVP:** lista de frases en datos, generadas con síntesis de voz y filtro de radio. Se pueden sustituir por grabaciones reales sin tocar código. En este equipo solo hay una voz en español (Helena, es-ES), por lo que las voces distintas saldrían de cambiar su tono. Es el punto más flojo del MVP: ver decisión D2.

---

## 8. Hackeos

Se lanzan desde el mapa o sobre el dispositivo en la imagen. Son el corazón del juego y **no deben ser triviales**: cuanto más importante el objetivo, más duro el puzzle. Todos se generan al azar y siempre tienen solución.

**Regla común:** tiempo límite + intentos contados. Cada fallo suma sospecha y cada puzzle cuesta el tiempo que estás sin ver nada. Si agotas los intentos, el sistema se bloquea 60 s y salta el +25.

| Objetivo | Efecto | Puzzle | Tiempo | Intentos |
|---|---|---|---|---|
| Cámara | Bucle de 40 s: no detecta a nadie | EMPALME | 12 s | 3 |
| Luces de una sala | Apagar o encender | PUENTE fácil | 20 s | 2 |
| Puerta electrónica | Abrir, o bloquearla a los guardias 30 s | PUENTE | 25 s | 2 |
| Panel de alarma del muelle (al entrar, hace de tutorial) | Entrada sin aviso | PARCHE fácil | 35 s | 3 errores |
| Lector biométrico de la oficina | Abre la oficina del director | HUELLA | 25 s | 2 |
| Sensores de la antesala | Apagados 60 s | CORTAFUEGOS | 40 s | 2 |
| **Cámara acorazada, fase 1** | Abre el objetivo | CRIPTA | 75 s | 6 códigos |
| **Cámara acorazada, fase 2** | La puerta no avisa al abrirse | PARCHE difícil | 35 s | 3 errores, sin reintento |
| Policía (una vez por partida) | Desvía a la unidad | INTERFERENCIA | Lo que dure el audio | 1 |

**Excepción de la fase 2:** si falla, la cámara se abre igual pero salta la alarma silenciosa y la policía pasa directamente a En camino. El robo sigue, ahora a contrarreloj y sin que ningún indicador te lo recuerde.

### 8.1 EMPALME (rápido, de reflejos)
Hay que igualar una onda objetivo girando tres diales (frecuencia, amplitud, fase) y **mantener** la coincidencia 1,5 s. Los diales derivan solos y la señal tiene ruido.

### 8.2 PUENTE (espacial)
Una rejilla de piezas de circuito que se giran para llevar corriente del origen a la salida, evitando los nodos de cortocircuito. Una línea de barrido cruza la rejilla y **gira las piezas por las que pasa**. Más difícil = rejilla mayor (5×5 a 8×8), más cortocircuitos, barrido más rápido.

### 8.3 CRIPTA (de razonamiento)
Deducir un código de 5 símbolos entre 8 posibles, sin repetir ninguno, con 6 intentos. Tras cada intento se indica cuántos están bien colocados y cuántos existen en otro lugar. Mientras tanto Liebre trabaja en la puerta y hace ruido, y no ves lo que ocurre fuera.

### 8.4 INTERFERENCIA (de oído)
Se suplanta a la central: suena una secuencia de 6–7 tonos entre ruido, una sola vez, y hay que repetirla en un teclado.
**Éxito:** *"Falsa alarma, cancelen"*, +120 s al tiempo de llegada. **Fallo:** la central sospecha, −30 s.
Solo sirve si escuchaste que venían. Si la lanzas en Calma, la gastas para nada.

### 8.5 CORTAFUEGOS (persecución)
Un laberinto informático en una matriz. Mueves tu paquete (una bolita) con el teclado y el cortafuegos (otra bolita) te persigue por el camino más corto.

- Hay que recoger **3 llaves de datos** repartidas y después llegar a la salida. Sin llaves, la salida no abre.
- El laberinto tiene ciclos, sin callejones sin salida largos: siempre se puede dar la vuelta y despistarlo. Sin eso, ser alcanzado dependería de la suerte.
- El perseguidor va al 85 % de tu velocidad y acelera con cada llave que recoges, hasta superarte al final.
- Si te alcanza, pierdes un intento y empiezas en un laberinto nuevo.
- Más difícil = matriz mayor o un segundo perseguidor.

### 8.6 HUELLA (de vista)
Arriba, la huella registrada, nítida. Abajo, 8 muestras borrosas y con ruido: solo una es de esa huella. Las otras 7 son muy parecidas, con el mismo patrón general y unos pocos puntos distintos.

- Hay que acertar **3 dedos seguidos**. Sin pistas parciales: eliges y confías en lo que has visto.
- Al pasar el ratón sobre una muestra se enfoca un poco, pero solo una a la vez, mientras corre el tiempo.
- La muestra correcta siempre se puede distinguir: el generador garantiza una diferencia mínima visible respecto a las falsas, por encima del desenfoque. Si no, sería adivinar.
- Un error gasta un intento y reinicia los 3 dedos.
- Más difícil = más desenfoque, más muestras, señuelos más parecidos.
- Las huellas se generan por adelantado en familias de texturas parecidas.

### 8.7 PARCHE (búsqueda bajo presión)
La justificación es que estás parcheando código en caliente. Una matriz de letras y números (12 filas × 32 columnas) donde **cada fila se desplaza a su propio ritmo y sentido**. A un lado, los 3 fragmentos que debes encontrar.

- Mueves un rectángulo del tamaño del fragmento con el teclado o el ratón, y confirmas con **Espacio**.
- Si lo que hay dentro coincide con un fragmento pendiente, ese queda parcheado y las filas se reordenan.
- Si no coincide, cuenta como error. Al tercero, falla.
- Cada fragmento existe una sola vez en la matriz, con **señuelos que difieren en un carácter**.
- Las filas son circulares: un fragmento puede cruzar el borde y el rectángulo también.
- Al confirmar se aceptan ~120 ms de margen, para que una fila en movimiento no castigue un acierto real.
- Fácil: 2 fragmentos de 4 caracteres. Difícil: 3 de 6, con filas más rápidas.

---

## 9. El edificio y los guardias

**Casa de subastas de una planta**, de noche. La camioneta está en el muelle de carga.

```
 Muelle ── Pasillo de servicio ── Cuarto eléctrico
              │         │
         Seguridad   Vestíbulo ── Galería Oeste ── Oficina del director
                        │                                  │
                   Galería Este ─────────────── Antesala ── Cámara acorazada
```

- **8 cámaras** más la exterior. El pasillo de servicio tiene zonas ciegas a propósito: la banda desaparece y solo queda la radio.
- **Objetivo:** panel de alarma del muelle → lector biométrico y tarjeta en la oficina → sensores de la antesala → cámara acorazada → botín → volver a la camioneta.
- **Rutas alternativas:** por las galerías (cámaras, más rápido) o por servicio (patrullas, zonas ciegas).

**Guardias: 3 de patrulla y 1 operador.**

- Cono de visión y oído reales. Estados: patrullar, investigar, alerta, fuera de combate.
- **El operador** mira las cámaras. Una cámara activa detecta a la banda en ~2 s. Si lo noquean, las cámaras dejan de ser peligrosas, pero él falta al siguiente control de radio.
- **Controles:** cada ~3 min cada guardia responde por radio. Si alguien no contesta, otro va a buscarle y la sospecha sube.
- Las patrullas varían entre partidas.

---

## 10. Fin de partida

- **Victoria:** la banda vuelve a la camioneta con el botín antes de la Llegada.
- **Derrota:** llega la policía con la banda dentro. Si cae toda la banda, los guardias llaman y es el mismo final.
- **Retirada sin botín:** se escapa, pero se cuenta como golpe fallido.
- Si Liebre cae, Yunque puede recoger el botín.

**Informe final:** ahora sí se revela todo. Línea de tiempo con cada error, qué sumó sospecha, cuándo salió la unidad y a qué distancia estuvo. Es la recompensa de la presentación: *"te delató el control que no contestó a las 02:21"*. Reinicio inmediato.

---

## 11. Dirección visual

### Recomendación: modelos 3D disfrazados de 2D

El aspecto objetivo es baldosas isométricas limpias y pintadas, en la línea de los juegos de supervivencia con baldosas, más refinado y sin detalle de ruido, para que el entorno se lea bien.

| | 2D total con sprites | **3D disfrazado (recomendado)** |
|---|---|---|
| Luz y oscuridad reales | Muy difícil | Directo |
| Personajes | 8 direcciones × cada animación, dibujados | Un modelo, todas las direcciones |
| Oclusión, muros cortados | A mano por baldosa | Se resuelve por cámara |
| Producción sin artista 2D | Inviable en MVP | Viable con Blender |
| Riesgo | Coste de arte | Que se note 3D |

### Cómo se evita que parezca 3D

- Cámara **ortográfica** a 30° de inclinación y 45° de giro (proporción 2:1, la clásica isométrica). Sin perspectiva, sin moverse.
- Render a baja resolución (640×360 inicial) y escalado por píxel entero, sin suavizado.
- Sin brillos, sin reflejos, sin mapas de normales. Texturas pintadas con la sombra de las esquinas incluida.
- Luz en 3–4 bandas planas por sala y sombras duras.
- Contorno fino oscuro en siluetas.
- **Los personajes giran a saltos en 8 direcciones y se animan a ~10 fps**, igual que un sprite.
- Muros cercanos a la cámara ocultos, dejando ver el interior.
- **Plan B:** si la prueba visual no convence, los mismos modelos se renderizan a hojas de sprites.

### Filtro de cámara

Cada cámara tiene un perfil propio: líneas de barrido, viñeta, ruido, desplazamiento de color, desaturación. Algunas a color apagado, otras en blanco y negro, y las IR en monocromo. El filtro ensucia, pero nunca debe impedir distinguir a un guardia de un ladrón con luz. Solo la oscuridad real quita la visión.

---

## 12. Técnica

- **Unity 6000.5.2f1**, URP, Input System nuevo. Un jugador, sin red.
- NavMesh para NPC. Sensores y lógica independientes de lo que se renderice: lo que ocurre en una cámara que no miras sigue ocurriendo.
- Solo se renderiza la cámara activa, a un `RenderTexture`.
- Modelos generados desde el editor con `MeshBuilder` (mallas de caras planas con el color en los vértices). Datos en ScriptableObjects: guardias, sospecha, puzzles, radio, filtros.

```
Assets/_Project/Scripts/
├── Core/        HeistDirector, reloj, eventos
├── Police/      PoliceResponse (oculto)
├── Security/    cámaras, sensores, puertas, RoomLighting
├── Npc/         CrewAgent, GuardAgent, sensores, órdenes
├── Hacking/     IHackPuzzle, Empalme, Puente, Cripta, Interferencia,
│                Cortafuegos, Huella, Parche
├── Radio/       RadioScheduler, canales, líneas, filtro
├── Feed/        render de la cámara, perfiles, estática
└── UI/          consola, mapa, informe
```

- `Dev`: panel solo de editor con la sospecha, el estado y el tiempo real. **Nunca en el build.**
- Pruebas automáticas: que los puzzles siempre tengan solución y que el estado de la policía transite como en 6.1.

---

## 13. Alcance

**Dentro del MVP:** todo lo descrito arriba, un edificio, dos ladrones, cuatro guardias, un botín.

**Si el tiempo aprieta, se corta primero (en este orden):** cámaras IR, eliminar (solo noquear), INTERFERENCIA, PUENTE (luces y puertas pasarían a EMPALME).

**Fuera del MVP:** más edificios o bandas, plantear el golpe antes de empezar, progresión y mejoras, guión con historia, suplantar los controles de radio de los guardias, multijugador, voces finales.

---

## 14. Plan de implementación

| Hito | Contenido | Se valida |
|---|---|---|
| **M0** | Proyecto Unity, una sala, filtro y pixelado | **Tú decides si se ve "no 3D"**. No se sigue sin este visto bueno. |
| **M1** | Edificio gris completo, consola, cambio de cámaras, luces reales | Navegar y que la oscuridad ciegue de verdad |
| **M2** | Banda, órdenes y guardias | Dirigir un paso completo |
| **M3** | Los siete puzzles | Que sean tensos y no triviales; que HUELLA sea difícil pero justa |
| **M4** | Policía oculta y radio | Saber el peligro solo escuchando |
| **M5** | Arte final, informe, ajuste | Partida completa presentable |

### Criterios de aceptación

- Una partida limpia dura entre 10 y 15 min y se puede ganar y perder.
- En una sala a oscuras no se puede saber si hay alguien, salvo por linterna, IR o radio.
- En ninguna pantalla hay un dato que delate el estado de la policía antes del informe.
- Un jugador que escucha el escáner anticipa la llegada mejor que uno que no.
- La cámara acorazada no se abre sin razonar; un jugador nuevo puede fallarla.
- Viendo capturas, la impresión es de juego 2D isométrico.

---

## 15. Decisiones a validar

Propuestas aceptadas en general el 2026-11-09. Falta el visto bueno para empezar M0.

| # | Decisión | Propuesta |
|---|---|---|
| **D1** | Ruta gráfica | 3D disfrazado, con M0 como prueba y sprites como plan B |
| **D2** | Voces de radio | Síntesis provisional con filtro; a la larga, grabar voces reales |
| **D3** | Qué pasa al fallar un hackeo | Sube la sospecha y puede bloquear el sistema; **no** es derrota inmediata |
| **D4** | Transcripción de radio | Sí, mientras suena y desactivable |
| **D5** | Escenario y banda | Casa de subastas, dos ladrones |
| **D6** | Idioma | Español |

## 16. Demo web (noviembre de 2026)

Al probar el MVP de Unity, los hackeos resultaron demasiado difíciles y dirigir a la banda exigía demasiado control. La demo de `Web/` prueba otra forma de jugar:

- **Banda autónoma:** se mueve, espera y neutraliza guardias por su cuenta.
- **Jugador de apoyo:** atiende peticiones, cada una con su botón.
- **Hackeos cortos:** el mundo va a cámara lenta mientras duran.

Cómo abrirla y qué cambia en detalle: `Web/README.md`.
