# Historial de cambios — Data Center Rescue

## v1.6.0 — Guía de jugador y UI frontal garantizada (actual)

- La guía del jugador y el briefing explican el objetivo general, el flujo de cinco pasos, las cinco incidencias, las auditorías y sus herramientas requeridas. Se aclara cómo leer el HUD, cuándo una estación acepta interacción, qué hacer ante una respuesta incorrecta y cuándo cerrar el turno.
- Los textos largos se muestran en una ventana desplazable dentro del menú. En PC se utiliza la rueda del ratón o Page Up/Page Down; en VR se utiliza el eje vertical del stick derecho. El indicativo de desplazamiento ocupa una banda entre el texto y los botones, sin tapar el contenido ni las opciones.
- Todos los elementos de interfaz runtime (menús, botones, guía, briefing, controles, opciones, HUD, objetivo, retícula y avisos) se dibujan en un Canvas Screen Space. En escritorio es Screen Space Overlay; en XR es Screen Space Camera con cámara URP Overlay apilada sobre la cámara del jugador, culling exclusivo de capa UI y limpieza de profundidad.
- Los botones están anclados a la franja inferior; el contenido descriptivo usa el panel central desplazable. La interfaz deja de depender de etiquetas 3D dentro de la sala y no se oculta detrás de racks ni servidores.
- Se mantiene la compatibilidad con PC y OpenXR, las cinco incidencias, las cinco auditorías y las treinta fases. No se agregan paquetes, modelos ni texturas externas.
- Se incorpora comprobación estática de render modes, pila de cámaras XR, capa UI, scroll del panel, atajos Page Up/Page Down y presencia de la guía. La compilación real debe ejecutarse con Unity 6000.6.0f1 mediante Tools/compile_check.ps1.

## v1.5.0 — Guía clara e interfaz frontal (anterior)

- Se migró toda la UI runtime a un Canvas único Screen Space: en PC usa `Screen Space - Overlay`, que se dibuja después de la escena y por encima de los assets.
- En VR/OpenXR usa `Screen Space - Camera` con una cámara URP `Overlay` añadida al `cameraStack` de la cámara principal. La cámara de UI solo renderiza la capa `UI` y limpia el depth antes de dibujar, de modo que racks, servidores y equipos no pueden tapar los paneles.
- Los botones de menú son gráficos UGUI en pantalla; ya no son cubos o paneles 3D colocados en la sala. Texto y botón comparten el mismo RectTransform, y el raycast UI conserva selección por ratón y mando XR.
- El marcador del objetivo deja de proyectarse encima de los modelos 3D y pasa a una banda fija del HUD, bajo el resumen superior. El prompt queda en el borde inferior y la retícula en el centro.
- El panel principal utiliza anchors normalizados y botones en bandas verticales relativas, para no depender de posiciones rígidas en unidades de pantalla.
- Se amplió la guía del jugador: objetivo, bucle de 5 pasos, lista de las cinco incidencias, herramientas para las auditorías, controles PC/VR y cierre del turno. Se accede desde menú, briefing y pausa; F1 abre la guía durante el juego.
- El briefing inicial incorpora el orden de trabajo y una ruta a la guía completa.
- Se reforzó el diagnóstico de UI: modo Canvas, cámara UI, camera stack XR, culling mask, depth clear, capa de cada Graphic, fuente y caracteres visibles.
- Se actualizó `README.md`, `GUIA_DEL_JUGADOR.md`, `VERSION_HISTORY.md`, `VERSION` y Player Settings a 1.5.0. No se incorporan assets ni paquetes externos.
- La comprobación de Editor batch incluye la creación de una pila URP Base + Overlay; la compilación real debe ejecutarse con Unity 6000.6.0f1.

## v1.4.6 — Tipografía mayor y HUD fuera del centro (anterior)

- Se aumentan los tamaños máximos de fuente en **3 px** y los mínimos de autoajuste en **2 px** para títulos, explicaciones, botones, HUD, avisos, retícula y rótulos. Los tamaños continúan centralizados en `DCRUIStyle.cs`.
- El HUD deja de ocupar la franja superior central: se ancla a la esquina superior izquierda y utiliza una placa oscura compacta. El aviso de interacción se mueve a la esquina inferior derecha con su propia placa, dejando despejada la zona central para observar los servidores y las consolas.
- Se reduce el tamaño físico del HUD y del prompt, y se conservan los cálculos según FOV y aspecto para mantenerlos dentro del área segura.
- Los rótulos de estaciones y terminales preventivos se hacen algo más compactos, se elevan sobre los equipos, incorporan un fondo oscuro discreto y se desplazan dinámicamente unos centímetros hacia la cámara para reducir la oclusión por racks/terminales.
- La retícula se acerca ligeramente al punto de vista; la interfaz sigue usando Canvas World Space y Unity UI Text para PC y OpenXR.
- No se agregan modelos, texturas, fuentes, sonidos ni paquetes externos. Las placas son primitivas generadas mediante código.
- El verificador estático comprueba el incremento exacto de fuente, la geometría de las dos esquinas para 63 combinaciones de aspecto/FOV, y la existencia de fondos y separación dinámica de rótulos. La compilación real y la prueba visual siguen requiriendo Unity Editor en el equipo destino.

## v1.4.5 — Hotfix de visibilidad UGUI y diagnóstico de Canvas

- Se reemplaza la creación manual de un `Material(Shader.Find("UI/Default"))` por `Canvas.GetDefaultCanvasMaterial()`, el material UGUI que Unity proporciona para dibujar elementos de Canvas. El material pertenece a Unity y no se destruye al descargar los recursos runtime.
- La fuente dinámica Liberation Sans permanece dentro de `Resources`; cada texto solicita sus glifos al Font antes de forzar la generación de `TextGenerator`, y cada Canvas se vincula a la cámara del jugador.
- La prueba de arranque valida el material real que entrega Canvas, el shader soportado, la textura de la fuente y los caracteres generados de cada texto visible del menú. Los errores muestran `DCR_UI_TEXT_FAILED` con el nombre del texto y los recursos que faltan.
- La prueba de Editor batch usa exactamente el material predeterminado de Canvas, un World Space Canvas y una cadena de caracteres españoles; solo emite éxito cuando se generan caracteres visibles y existe la textura dinámica.
- Se corrigió el orden de renderizado para que los rótulos de terminales (sorting order 100) queden detrás del menú (110), HUD (120), prompt (121) y retícula (122).
- Se conserva toda la jugabilidad de v1.4.4: rig PC/XR, movimiento, cinco incidencias, cinco auditorías, treinta fases y controles.
- No se añaden paquetes, modelos ni texturas externas. La validación estática se repite sobre una extracción limpia del ZIP. La compilación real de Unity y la prueba visual en visor siguen requiriendo Unity Editor/runtime en el equipo destino.

## v1.4.4 — Migración UGUI inicial con texto aún no visible (supersedida en v1.4.5)

- Se reemplazó la capa de texto runtime del juego por `UnityEngine.UI.Text` sobre Canvas en modo World Space. La sala, la locomoción, las misiones, las estaciones, los botones 3D y el flujo de interacción se conservan.
- Cada `Text` recibe explícitamente un material compartido creado manualmente con `Shader.Find("UI/Default")`; el atlas dinámico sigue procediendo de la fuente TTF Liberation Sans incluida en `Assets/DCRRuntimeUI/Resources`.
- Se añadió una fuente dinámica propia del proyecto con licencia OFL. Si Unity no puede cargarla, se intenta usar Arial/LegacyRuntime como respaldo.
- Se añadió validación runtime después de construir el menú inicial para comprobar Canvas World Space, cámara, fuente, material, textura, tamaño de RectTransform y caracteres generados. La revisión posterior de v1.4.5 sustituyó el material manual por el material oficial de Canvas.
- El verificador batch de Unity crea un Canvas World Space real de prueba, genera texto español con la misma fuente y material, y verifica el shader, la textura y los caracteres antes de emitir el marcador de éxito.
- Se corrigió el propio verificador estático para comprobar la fábrica UGUI real y mostrar fallos en vez de detenerse por una variable de test inexistente.
- Se conserva la escala de texto por rol, los anchors centrados, el ajuste por FOV/aspecto, los 30 pasos de mantenimiento y la interacción PC/XR.
- No se incorporan modelos, texturas, escenas ni paquetes externos. Esta revisión elimina la dependencia de TMP para la UI de juego; los recursos de TMP que acompañan al proyecto pueden permanecer para otros assets, pero ya no controlan estos textos.
- Las pruebas estáticas y la prueba de recursos por Editor están preparadas. La compilación real debe ejecutarse con Unity 6000.6.0f1; este entorno no contiene Unity Editor.


Este registro describe las funciones del **proyecto fuente** por versión. No significa que cada versión tenga un ejecutable ya compilado o una prueba física de visor.

## v1.4.3 — Migración inicial UGUI (intento no distribuido)

- Se reemplazó TMP en los textos generados en runtime por Unity UI Text sobre Canvas World Space.
- Esta iteración no asignaba un material de texto explícito ni verificaba que la textura dinámica del Font llegara al renderer; quedó como intento intermedio y se corrigió en v1.4.4.

## v1.4.2 — Intento intermedio de reparación TMP/URP (supersedido en v1.4.4)

- Se identificó una incompatibilidad relevante: el font asset LiberationSans SDF usaba el material clásico `TextMeshPro/Mobile/Distance Field`, mientras el proyecto utiliza Universal Render Pipeline.
- Se añadió a `Resources` una copia del material Shader Graph TMP URP incluido con TextMesh Pro (`TMP_SDF-URP Unlit`), sin descargar paquetes ni recursos externos.
- En tiempo de ejecución se crea una instancia de ese material URP y se asigna el atlas SDF real de LiberationSans mediante `TMP_FontAsset.atlasTexture`; se copian los parámetros de SDF necesarios y se conserva el material original del asset sin modificarlo.
- Se restauraron exactamente los rangos de tamaño de TextMeshPro de la primera versión jugable conocida (título 2.15, cuerpo 1.02, botones 1.08, HUD 0.88, aviso 0.92, retícula 1.10 y rótulos 0.96), en lugar de reducirlos aún más.
- La inicialización registra fuente, atlas, material y shader; cada texto no vacío fuerza la generación de malla y registra un error si no genera caracteres o carece de renderer/material.
- El comprobador batch de Unity ahora valida la fuente, atlas, recurso `Resources`, material URP y la asignación del atlas antes de emitir el marcador de éxito.
- Se conserva la jugabilidad de 30 decisiones y el diseño responsivo. No se añadieron modelos ni paquetes externos.
- La validación estática no equivale a una compilación real ni a una prueba visual; estas requieren ejecutar Unity 6000.6.0f1.

## v1.4.0 — Usabilidad, claridad de interacción y UI revisada

### Interacción y jugabilidad legible
- Cada botón crea su propia etiqueta como hijo del objeto clicable. La etiqueta comparte el centro del collider y compensa la escala geométrica del botón; deja de depender de una segunda posición manual.
- Los textos de título, descripción y botones se alinean al centro del panel. HUD, mensajes contextuales y etiquetas de estaciones también usan alineación centrada.
- Los botones de Configuración muestran el valor actual que van a modificar: sensibilidad, volumen, inversión vertical, giro VR y velocidad.
- Las opciones de Pausa, briefing, historial y vuelta a la sala tienen nombres que describen su acción; el botón de salida de una auditoría explica que conserva las fases validadas.
- El briefing explica el ciclo de juego en orden: seguir el objetivo, acercarse, apuntar, abrir, leer síntoma/acción/motivo y elegir una respuesta.
- Las instrucciones de PC y VR se separan y explican cómo navegar por menús, moverse, cambiar herramientas, interactuar y pausar.
- El HUD resume objetivo, distancia/dirección, incidencia/paso, tiempo, puntuación, errores, herramienta equipada y avance preventivo.
- El prompt contextual identifica la consola/terminal, indica qué botón usar y, si falta una herramienta, explica cómo equiparla en PC o VR.
- La explicación de las 30 fases se reorganiza en síntoma/objetivo/acción/seguridad para evitar párrafos técnicos innecesariamente largos.
- Se evita reasignar el texto del HUD y del aviso en cada frame si su contenido no ha cambiado.

### Sistema visual responsivo
- `DCRUIStyle.cs` es la fuente única para tamaños de fuente TMP, dimensiones del panel, botones, HUD, prompt y márgenes seguros.
- Los tamaños de fuente se centralizan por rol y usan autoajuste dentro de cada caja.
- Los textos de botones se alojan como hijos del botón y usan una escala inversa para conservar las dimensiones del panel al estar dentro del cubo clicable.
- Panel, HUD y prompt calculan su escala de forma independiente según el FOV y el aspecto de la cámara.
- Se amplió el área de contenido para evitar que los botones invadan el texto explicativo. Los textos largos pueden autoajustarse y usan salto de línea y elipsis como último recurso.
- El ajuste geométrico se vuelve a calcular al cambiar resolución, aspecto o FOV.

### Versionado/documentación
- La versión del Player Settings y el menú de Editor se actualizan a `1.4.0`.
- Se añade `VERSION_HISTORY.md` con una comparación explícita de capacidades por versión, incluyendo que la v1.0 no tenía movimiento de jugador propio.
- README, bitácora del juego e historial se alinean con la implementación real.
- No se incorporan modelos, texturas, audio, escenas ni paquetes externos nuevos.

## v1.4.1 — Hotfix intermedio de visibilidad TMP (supersedido en v1.4.4)

- Se corrigió la regresión de tipografía de v1.4.0: los tamaños 8–22 eran desproporcionados para los objetos TextMeshPro 3D del juego. Se vuelven a rangos espaciales pequeños y específicos por rol.
- Se asigna explícitamente la fuente `LiberationSans SDF` desde Resources y su material; TMP Settings queda como respaldo.
- Se fuerza la generación inicial de malla, se habilita el Renderer y se eliminan sombras para todos los textos creados por código.
- Se retiró el cambio de anchors de los textos 3D, manteniendo sus posiciones locales y cajas de tamaño conocidas.
- Se movieron títulos, descripciones y etiquetas hacia la cámara para evitar oclusión por cubos opacos.
- Se sustituyeron flechas no incluidas en la fuente por palabras de orientación compatibles con el atlas.
- Se añadió diagnóstico al arranque y validación de la fuente, material, shader y caracteres.
- No se incorporaron paquetes ni assets gráficos externos.

## v1.3.0 — Más procedimientos y libre recorrido

- Cinco auditorías preventivas con tres fases cada una: 15 fases opcionales, que sumadas a las 15 decisiones principales dan 30 decisiones.
- La herramienta adecuada es requisito para abrir cada terminal: escáner de red, kit ESD, anemómetro, multímetro o tablet.
- El avance preventivo se conserva al salir de un terminal y volver después.
- Cada fase correcta queda en la bitácora; cada terminal tiene tres indicadores luminosos generados por código.
- Después de restaurar DB-03, el jugador puede completar auditorías pendientes antes de cerrar el turno.
- Cada auditoría otorga 120 puntos; el máximo teórico queda expresado sobre 1600 puntos antes de penalizaciones.
- Se centralizaron los primeros estilos TMP y se adaptó por separado la escala de panel, HUD y prompt.

## v1.2 — Herramientas y mantenimiento preventivo

- Se añadieron cinco herramientas seleccionables y cinco terminales preventivos construidos con primitivas.
- Se añadieron bitácora operativa, puntuación de auditorías, historial de versiones y ajustes persistentes.
- Se ampliaron los menús de pausa, opciones y controles.

## v1.1 — Primera versión jugable

- Se añadió movimiento en PC (WASD/flechas y ratón) y seguimiento/locomoción de entrada XR.
- Se añadió interacción por rayo con consolas, menú principal, briefing, pausa, controles y configuración.
- Se añadieron cinco incidencias principales de tres pasos cada una (15 decisiones), temporizador, errores, puntuación y resultados.

## v1.0 — Proyecto base recibido

- Incluía el entorno 3D de sala de servidores, racks, climatización, iluminación, materiales y una cámara de demostración.
- No incluía scripts propios `DCRPlayerController`, `DCRGameManager` o `DCRRuntimeBootstrap`; la escena `DataCenter.unity` configurada para arrancar contenía la cámara, luz, volumen y suelo básicos.
- Por lo tanto, **no había movimiento de personaje implementado por el proyecto**, menú de juego, misiones ni procedimientos interactivos. Era principalmente un entorno 3D de demostración.

## Siguiente trabajo propuesto (no implementado)

### v1.7.0 — Manipulación y diagnóstico físico simulado
- Paneles de estado que cambien durante cada incidencia.
- Puntos de prueba para conectividad, temperatura y alimentación.
- Cables, bandejas y componentes con interacción sencilla basada en primitivas y animaciones por código.

### v1.8.0 — Rejugabilidad
- Variantes de síntomas, modo práctica sin temporizador y modo evaluación.
- Inventario simple de repuestos y una evaluación por seguridad, tiempo y evidencia.

Las funciones de v1.7.0 y v1.8.0 son propuestas futuras, no forman parte de esta entrega.


