# Data Center Rescue

Simulador didáctico de mantenimiento de un centro de datos, desarrollado con Unity 6.6 y URP. El proyecto conserva la sala 3D original de iPoly3D y añade un turno de trabajo completo con procedimientos, interacción, puntuación y soporte de entrada XR mediante OpenXR.

## Qué incluye

- Menú principal y briefing antes de comenzar el turno.
- Cinco incidencias: diagnóstico de base de datos, sustitución de SSD, recuperación de climatización, revisión de UPS/PDU y validación final.
- Tres decisiones por incidencia. Las respuestas incorrectas penalizan la puntuación, pero permiten reintentar el paso.
- Cinco auditorías preventivas opcionales de tres fases cada una (15 fases adicionales), con herramientas específicas y avance conservado al salir del panel.
- Tras restaurar DB-03, se habilita una fase de libre recorrido para completar las auditorías pendientes; el jugador decide cuándo cerrar el turno desde Pausa.
- Treinta decisiones de mantenimiento en total: 15 pasos principales y 15 preventivos. Completar cada auditoría otorga 120 puntos.
- Tres indicadores luminosos por terminal muestran el avance de cada auditoría en el entorno 3D.
- Estaciones de servicio señalizadas dentro de la sala 3D, paneles de procedimiento y HUD frontal.
- Temporizador, errores, puntuación, calificación final, récord y turnos completados guardados localmente.
- Señales sonoras breves para selección, acierto y error; el volumen general las controla desde configuración.
- Pausa, reinicio, bitácora de turno, historial de versiones, controles y configuración persistente de sensibilidad, volumen, velocidad, inversión vertical y giro por pasos en VR.
- UI completamente frontal en v1.6.0: en PC todos los menús, botones, HUD, avisos y objetivo se dibujan con UGUI (`UnityEngine.UI.Text`/`Image`) en un Canvas `Screen Space - Overlay`, por encima de la escena. En VR/OpenXR se utiliza un Canvas `Screen Space - Camera` asociado a una cámara URP `Overlay` apilada después de la cámara principal; esta cámara solo dibuja la capa `UI` y limpia la profundidad, evitando que los assets 3D tapen la interfaz. Ningún menú o botón se coloca físicamente entre los racks. El objetivo es una banda fija del HUD, no una etiqueta proyectada encima de un servidor.
- Los botones de Configuración muestran los valores actuales; los prompts indican qué acción hacer y qué herramienta falta, tanto en PC como en VR.
- La guía del jugador explica el bucle de juego, las cinco incidencias, las herramientas de las auditorías, los controles PC/VR y el cierre de turno. Está disponible en el menú, briefing y pausa; F1 abre la guía durante el juego. Cada procedimiento presenta síntoma, acción recomendada y motivo de seguridad.
- No se agregaron modelos, texturas de interfaz ni paquetes externos en v1.4.5, v1.4.6 ni v1.6.0. La fuente TTF está incluida en el proyecto; terminales e indicadores se construyen con primitivas en tiempo de ejecución.
- Control de escritorio para pruebas sin visor y lectura de cabeza/mandos XR cuando hay un runtime OpenXR activo. En VR, el movimiento sigue la dirección horizontal de la cabeza.

> El proyecto no controla hardware real. Los procedimientos son una simulación educativa y las estaciones de servicio son elementos interactivos creados en tiempo de ejecución.
>
> La versión fuente actual es **1.6.0**. Para ver qué cambió entre versiones y comprobar si la v1.0 permitía movimiento, consulta [la tabla comparativa de versiones](VERSION_HISTORY.md) y el [CHANGELOG](CHANGELOG.md). También puedes consultar la [Guía del jugador](GUIA_DEL_JUGADOR.md).

## Requisitos

- Unity **6000.6.0f1** (versión con la que se guardó el proyecto).
- Módulo **Windows Build Support (IL2CPP)** para exportar a Windows.
- Para Meta Quest u otro visor Android: Android Build Support, Android SDK & NDK Tools y OpenJDK instalados desde Unity Hub.
- Un runtime OpenXR funcional (por ejemplo, Meta Quest Link/Air Link o SteamVR para PCVR).
- Paquetes directos fijados en `Packages/manifest.json`: Input System 1.20.0, URP 17.6.0, UGUI 2.6.0, XR Plug-in Management 4.7.0 y OpenXR 1.19.0.

Al abrir por primera vez, Unity Package Manager debe resolver las dependencias de `Packages/manifest.json`. El manifiesto se ha reducido a los paquetes utilizados por el proyecto; no declara módulos internos de Unity ni dependencias de Timeline/XR Interaction Toolkit que no se usan.

## Abrir y probar en PC sin visor

1. Descomprime el ZIP.
2. En Unity Hub selecciona **Add > Add project from disk** y elige la carpeta `Data_Center_Rescue-main` (la carpeta que contiene `Assets`, `Packages` y `ProjectSettings`).
3. Abre el proyecto con Unity 6000.6.0f1 y espera a que termine la importación.
4. Abre `Assets/Scenes/DataCenter_Demo.unity`.
5. En el menú superior de Unity selecciona **Data Center Rescue > Set Playable Scene as Startup**. Esto deja la sala completa como primera escena de Build Settings y actualiza la versión del Player Settings a 1.6.0.
6. Presiona **Play**. El menú principal aparece delante de la cámara.

### Controles de escritorio

| Acción | Control |
|---|---|
| Mirar | Mouse |
| Caminar | W, A, S, D o flechas |
| Correr | Shift |
| Interactuar / seleccionar | E o clic izquierdo |
| Pausar / cerrar panel | Esc |
| Confirmar en el briefing | Enter |

En los menús mueve el cursor sobre el botón y haz clic; también puedes recorrer las opciones con **↑/↓** o **W/S** y confirmar con **ENTER**; la opción resaltada es la seleccionada. El nombre de cada botón indica su acción. Durante el turno el cursor se bloquea y el centro funciona como punto de mira: sigue la flecha del HUD, acércate a la consola azul y pulsa **E**. En una auditoría, el aviso inferior indica el equipo requerido; equípalo con **1-5** y pulsa **E** para abrir el terminal. Cada fase ofrece tres acciones y una opción para volver a la sala conservando el progreso.

## Activar VR (OpenXR)

Los paquetes XR quedan declarados en el manifest, pero Unity exige habilitar el proveedor para cada plataforma desde el Editor. Sigue estos pasos después de abrir el proyecto y dejar que Package Manager resuelva los paquetes:

1. Conecta/enciende el visor y abre su runtime OpenXR.
2. En el menú superior de Unity ejecuta **Data Center Rescue > Enable OpenXR for PC + Android**. El asistente intenta crear los ajustes XR y asignar el cargador OpenXR para Standalone y Android.
3. Abre **Edit > Project Settings > XR Plug-in Management** y verifica que **OpenXR** esté activado para **PC, Mac & Linux Standalone**. Si no aparece activado, actívalo manualmente.
4. Para Meta Quest autónomo, cambia la plataforma a **Android** y activa también **OpenXR** en esa pestaña. Asegúrate de tener los módulos Android instalados desde Unity Hub.
5. En **OpenXR > Interaction Profiles**, habilita el perfil del mando que corresponda al visor (por ejemplo, Oculus Touch Controller Profile o Khronos Simple Controller, según el dispositivo/runtime).
6. Abre **Data Center Rescue > Open XR Plug-in Settings** si deseas regresar rápidamente a los ajustes XR.
7. Ejecuta la escena. La cámara lee la pose de cabeza; stick izquierdo desplaza, stick derecho gira por pasos y el gatillo derecho selecciona.

El juego usa entrada XR de bajo nivel (`UnityEngine.XR`) desde un rig creado en tiempo de ejecución. No necesita prefabs de XR Interaction Toolkit para la locomoción e interacción implementadas en esta versión. Para las auditorías preventivas, cambia de herramienta con el botón secundario izquierdo y apunta al terminal; el gatillo derecho selecciona.

## Solución del error del Package Manager

El ZIP ya no declara `com.unity.modules.terrain`, `com.unity.modules.timelinefoundation` ni `com.unity.timeline`; tampoco conserva el `packages-lock.json` anterior. Unity debe generar un lockfile nuevo. Si Package Manager sigue mostrando esos mismos paquetes, primero confirma que abriste la carpeta de este ZIP corregido y que el Editor utilizado corresponde a Unity 6000.6.0f1.

Con Unity cerrado, abre PowerShell en la raíz del proyecto y ejecuta:

```powershell
Remove-Item .\Packages\packages-lock.json -Force -ErrorAction SilentlyContinue
Remove-Item .\Library\PackageCache -Recurse -Force -ErrorAction SilentlyContinue
```

Luego vuelve a abrir el proyecto desde Unity Hub y deja que Package Manager regenere las dependencias. Necesitas conexión a `packages.unity.com` para descargar paquetes que no vienen embebidos en el Editor. No pulses Continue como solución permanente si todavía hay dependencias sin resolver.

## Correcciones de compilación (Unity 6000.6)

La corrección de versiones anteriores (1.4.0) resolvió los errores reportados en `DCRPlayerController.cs` y `DCRGameManager.cs`:

- Se usa el alias `XRCommonUsages = UnityEngine.XR.CommonUsages` en los dos scripts que importan simultáneamente `UnityEngine.InputSystem` y `UnityEngine.XR`. Las referencias de seguimiento, botones, gatillos y sticks apuntan ahora explícitamente a la API XR.
- En el hotfix de interfaz v1.4.0 se corrigió el uso obsoleto de `TMP_Text.enableWordWrapping`; desde v1.4.3 la UI runtime migró a UGUI `Text` y esa API de TMP ya no forma parte de la ruta de interfaz.

### Diagnóstico del mensaje de Burst

El mensaje `Can't find file \\.\pipe\unity-burst-...` procede del servidor de compilación de Burst, no de `CommonUsages` ni de TextMesh Pro. Este proyecto no declara jobs propios con `[BurstCompile]`, `IJob` o `Unity.Jobs`; Burst es un componente auxiliar de compilación, no el compilador C# principal. Unity 6000.6 incluye soporte Burst integrado y la versión 1.8.28 está publicada para esta versión del Editor.

Si el mensaje de Burst sigue apareciendo después de resolver los errores C#:

1. Cierra Unity Editor.
2. En PowerShell, desde la raíz del proyecto, ejecuta:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\reset_burst_cache.ps1
```

3. Abre de nuevo el proyecto y deja que Unity regenere la caché.
4. Si el servidor sigue sin arrancar, abre **Jobs > Burst** y desmarca temporalmente **Enable Compilation** para aislar el servidor. El código de gameplay de este proyecto no depende de Burst. Si eso elimina el mensaje, revisa el registro de Windows Security/antivirus para ver si bloqueó el ejecutable firmado de Burst; no desactives la protección global del equipo.

El script `reset_burst_cache.ps1` solo elimina `Library/BurstCache`, y se niega a ejecutarse si detecta Unity Editor en ejecución. No elimina `Assets`, `Packages`, escenas ni ajustes.

## Validación incluida

El proyecto incluye `Tools/validate_project.py`. Desde la raíz, ejecútalo con:

```powershell
python Tools/validate_project.py
```

El script realiza comprobaciones estáticas de manifiesto, escenas, GUID, estructura de C#, alias XR, dependencias, recursos UGUI, materiales Canvas, generación de glifos y las 30 fases de mantenimiento. El resultado de esta revisión se indica en `VALIDATION_REPORT.md`. Esto no sustituye la compilación del Editor ni una prueba en el visor.

### Compilación real en Unity (batchmode)

Con Unity cerrado, abre PowerShell en la raíz del proyecto y ejecuta:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\compile_check.ps1
```

Si Unity Hub está instalado en una ruta personalizada, indica el ejecutable:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\compile_check.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
```

El script abre el proyecto con Unity en `-batchmode`, espera la importación/compilación de assemblies y comprueba el marcador `DCR_BATCH_COMPILE_VALIDATION_PASSED`. Si hay errores C# o no aparece el marcador, devuelve código de salida distinto de cero. El log completo queda en `Tools/CompileCheck.log`. Ejecuta esta comprobación antes de crear una build de jugador.

## Crear una build

### Windows / PCVR

1. Abre **File > Build Profiles** (o Build Settings, según la vista del Editor).
2. Selecciona **Windows, Mac, Linux** y confirma que `DataCenter_Demo.unity` esté en la lista de escenas.
3. Selecciona **Build** y guarda el ejecutable en una carpeta nueva, por ejemplo `Builds/Windows/DataCenterRescue.exe`.
4. Para PCVR, verifica que OpenXR esté habilitado en Standalone y que el runtime del visor esté activo.

### Android / Meta Quest

1. En Unity Hub instala Android Build Support, Android SDK & NDK Tools y OpenJDK para Unity 6000.6.
2. En **File > Build Profiles**, selecciona Android y cambia la plataforma.
3. Activa OpenXR para Android en XR Plug-in Management.
4. Configura orientación **Landscape**, arquitectura **ARM64** y un identificador de paquete único en Player Settings.
5. Conecta el visor por USB, habilita Developer Mode y utiliza **Build And Run**.

## Arquitectura del código añadido

- `Assets/Scripts/DCRRuntimeBootstrap.cs`: instala los sistemas al cargar la escena.
- `Assets/Scripts/DCRGameManager.cs`: misiones, procedimientos, estados, puntuación, HUD, menús y opciones.
- `Assets/Scripts/DCRPlayerController.cs`: locomoción de teclado/mouse y pose/mandos XR.
- `Assets/Scripts/DCRWorldInteractable.cs`: estaciones, botones y etiquetas orientadas hacia la cámara. Los rótulos de botón se construyen como hijos del botón clicable desde `DCRGameManager`.
- `Assets/Editor/DCRProjectSetup.cs`: comandos de Editor para preparar la escena de inicio y abrir XR Plug-in Management.

La escena `DataCenter.unity` original se conserva como respaldo. La escena de inicio se cambió a `DataCenter_Demo.unity`, que es la que contiene la sala completa.

## Limitaciones conocidas / siguiente iteración

- Este ZIP contiene el proyecto fuente depurado, no un `.exe` o `.apk` ya compilado. La compilación final debe ejecutarse desde Unity Editor en el equipo de destino.
- No hay una prueba ejecutada en un visor físico desde este entorno; la compatibilidad final depende del runtime OpenXR, los perfiles de mando y la plataforma de compilación elegida.
- La interacción de mantenimiento se realiza mediante selección por rayo y paneles espaciales, no mediante agarre físico de herramientas o extracción animada de componentes.
- Las auditorías se resuelven por selección de acciones técnicas seguras en tres fases; todavía no simulan señales eléctricas/de red físicas ni manipulación de conectores.
- Consulta `VERSION_HISTORY.md` para la tabla comparativa: la versión inicial era una sala de demostración sin movimiento de personaje propio; la locomoción PC/VR se añadió en v1.1. Consulta `CHANGELOG.md` para los cambios de v1.4 y la hoja de ruta.
- El proyecto crea las estaciones y la interfaz durante la ejecución; por eso no es necesario arrastrar objetos manualmente a la escena.


En la versión 1.6.0, los paneles largos tienen desplazamiento integrado: rueda del ratón o Page Up/Page Down en PC, y stick derecho arriba/abajo en VR. Los botones permanecen anclados en la franja inferior.
