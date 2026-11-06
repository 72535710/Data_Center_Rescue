using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.Rendering.Universal;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace DataCenterRescue
{
    /// <summary>
    /// Runtime mission flow, screen-space UI, interaction, scoring and settings.
    /// Desktop UI uses Screen Space Overlay; XR UI uses a URP overlay camera stack.
    /// </summary>
    public sealed class DCRGameManager : MonoBehaviour
    {
        private enum ScreenMode { MainMenu, Tutorial, PlayerGuide, Options, Controls, Gameplay, Procedure, SideTask, OperationsLog, VersionHistory, Pause, Results }

        private sealed class ProcedureStep
        {
            public string Heading;
            public string Instruction;
            public string Correct;
            public string[] Incorrect;
            public string Rationale;

            public ProcedureStep(string heading, string instruction, string correct, string wrongA, string wrongB)
            {
                Heading = heading;
                Instruction = instruction;
                Correct = correct;
                Incorrect = new[] { wrongA, wrongB };
                Rationale = DCRGameManager.GetStepRationale(heading);
            }
        }

        private sealed class MissionDefinition
        {
            public string Title;
            public string Description;
            public ProcedureStep[] Steps;

            public MissionDefinition(string title, string description, params ProcedureStep[] steps)
            {
                Title = title;
                Description = description;
                Steps = steps;
            }
        }

        private sealed class SideTaskDefinition
        {
            public string Title;
            public string RequiredTool;
            public ProcedureStep[] Steps;

            public SideTaskDefinition(string title, string requiredTool, params ProcedureStep[] steps)
            {
                Title = title;
                RequiredTool = requiredTool;
                Steps = steps ?? Array.Empty<ProcedureStep>();
            }
        }

        private static readonly string[] ToolNames =
        {
            "ESCÁNER DE RED", "KIT ESD", "ANEMÓMETRO", "MULTÍMETRO", "TABLET DE SERVICIO"
        };

        private ScreenMode mode = ScreenMode.MainMenu;
        private ScreenMode optionsReturnMode = ScreenMode.MainMenu;
        private bool initialized;
        private bool mainServiceComplete;
        private Camera playerCamera;
        private Camera uiRenderCamera;
        private Camera xrOverlayCamera;
        private UniversalAdditionalCameraData baseCameraData;
        private DCRPlayerController playerController;
        private CharacterController characterController;
        private Transform playerRig;
        private Vector3 initialRigPosition;
        private Quaternion initialRigRotation;
        private Canvas overlayCanvas;
        private GraphicRaycaster overlayRaycaster;
        private ScrollRect bodyScrollRect;
        private EventSystem uiEventSystem;
        private PointerEventData uiPointerData;
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private GameObject panelRoot;
        private GameObject hudRoot;
        private GameObject promptRoot;
        private RectTransform objectiveMarkerRoot;
        private RectTransform xrMenuCursorRoot;
        private Text objectiveMarkerText;
        private Text hudText;
        private Text promptText;
        private Text crosshairText;
        private ScreenMode guideReturnMode = ScreenMode.Gameplay;
        private bool lastCanvasXRMode;
        private LineRenderer xrPointer;
        private AudioSource feedbackSource;
        private AudioClip successTone;
        private AudioClip errorTone;
        private AudioClip clickTone;
        private readonly List<Material> runtimeMaterials = new List<Material>();
        private DCRWorldButton hoveredButton;
        private DCRMaintenanceAction hoveredMaintenanceAction;
        private readonly List<DCRWorldButton> panelButtons = new List<DCRWorldButton>();
        private int keyboardMenuSelection;
        private DCRStation[] stations = new DCRStation[4];
        private DCRMaintenanceAction[] maintenanceActions = new DCRMaintenanceAction[5];
        private DCRStation activeProcedureStation;
        private SideTaskDefinition[] sideTasks;
        private int selectedToolIndex;
        private int activeSideTaskIndex = -1;
        private int sideTaskCorrectChoiceIndex;
        private readonly int[] sideTaskProgress = new int[5];
        private readonly string[] sideTaskChoices = new string[3];
        private int bonusTasksCompleted;
        private ScreenMode logReturnMode = ScreenMode.MainMenu;
        private readonly List<string> sessionLog = new List<string>();
        private bool lastXRToolCycle;
        private float lastResponsiveAspect = -1f;
        private float lastResponsiveFov = -1f;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private string[] currentChoices = new string[3];
        private int correctChoiceIndex;
        private MissionDefinition[] missions;
        private int currentMission;
        private int currentStep;
        private int mistakes;
        private int score = 1000;
        private float elapsedSeconds;
        private float lookSensitivity;
        private float masterVolume;
        private float walkSpeed;
        private float snapTurnDegrees;
        private bool invertY;
        private bool lastXRTrigger;
        private bool lastXRMenuButton;
        private string feedback = string.Empty;
        private float feedbackUntil;
        private bool cursorIsLocked;
        private bool menuNavigationActivated;
        private const float InteractionRange = 4.0f;

        public bool CanMovePlayer => initialized && mode == ScreenMode.Gameplay;
        public bool CursorIsLocked => cursorIsLocked;
        public float LookSensitivity => lookSensitivity;
        public float MoveSpeed => walkSpeed;
        public float SnapTurnDegrees => snapTurnDegrees;
        public bool InvertY => invertY;

        private void Awake()
        {
            LoadSettings();
        }

        public void Initialize(Camera sceneCamera)
        {
            if (initialized || sceneCamera == null) return;

            playerCamera = sceneCamera;
            SetupPlayerRig();
            CreateOverlayCanvas();
            BuildMissionDefinitions();
            BuildSideTaskDefinitions();
            CreateStations();
            CreateMaintenanceActions();
            CreateHUD();
            initialized = true;
            ShowMainMenu();
            StartCoroutine(ValidateRuntimeTextResourcesNextFrame());
            Application.targetFrameRate = 90;
            Application.runInBackground = true;
        }

        private void SetupPlayerRig()
        {
            Vector3 originalPosition = playerCamera.transform.position;
            float originalYaw = playerCamera.transform.eulerAngles.y;

            GameObject rigObject = new GameObject("DCR_PlayerRig");
            playerRig = rigObject.transform;
            playerRig.position = new Vector3(originalPosition.x, 0f, originalPosition.z);
            playerRig.rotation = Quaternion.Euler(0f, originalYaw, 0f);
            initialRigPosition = playerRig.position;
            initialRigRotation = playerRig.rotation;

            playerCamera.transform.SetParent(playerRig, false);
            playerCamera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.fieldOfView = 76f;
            playerCamera.nearClipPlane = DCRUIStyle.XRCameraNearClip;
            playerCamera.farClipPlane = 120f;
            playerCamera.stereoTargetEye = StereoTargetEyeMask.Both;

            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener == null) playerCamera.gameObject.AddComponent<AudioListener>();

            characterController = rigObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.27f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.slopeLimit = 48f;
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.035f;

            playerController = rigObject.AddComponent<DCRPlayerController>();
            playerController.Initialize(this, playerCamera, characterController);

            xrPointer = rigObject.AddComponent<LineRenderer>();
            xrPointer.positionCount = 2;
            xrPointer.useWorldSpace = true;
            xrPointer.startWidth = 0.006f;
            xrPointer.endWidth = 0.002f;
            xrPointer.enabled = false;
            xrPointer.material = CreateMaterial(new Color(0.1f, 0.85f, 1f, 1f));

            feedbackSource = rigObject.AddComponent<AudioSource>();
            feedbackSource.playOnAwake = false;
            feedbackSource.spatialBlend = 0f;
            feedbackSource.volume = 0.45f;
            successTone = CreateTone("DCR_Success", 740f, 0.13f, 0.24f);
            errorTone = CreateTone("DCR_Error", 190f, 0.2f, 0.24f);
            clickTone = CreateTone("DCR_Click", 520f, 0.055f, 0.12f);
        }

        private static AudioClip CreateTone(string clipName, float frequency, float duration, float amplitude)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float fadeIn = Mathf.Clamp01(time * 35f);
                float fadeOut = Mathf.Clamp01((duration - time) * 18f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * amplitude * fadeIn * fadeOut;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayFeedback(AudioClip clip)
        {
            if (feedbackSource != null && clip != null)
                feedbackSource.PlayOneShot(clip);
        }

        private void LoadSettings()
        {
            lookSensitivity = PlayerPrefs.GetFloat("DCR_Sensitivity", 0.085f);
            masterVolume = PlayerPrefs.GetFloat("DCR_Volume", 0.8f);
            walkSpeed = PlayerPrefs.GetFloat("DCR_WalkSpeed", 3.2f);
            snapTurnDegrees = PlayerPrefs.GetFloat("DCR_SnapTurn", 45f);
            invertY = PlayerPrefs.GetInt("DCR_InvertY", 0) == 1;
            AudioListener.volume = masterVolume;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat("DCR_Sensitivity", lookSensitivity);
            PlayerPrefs.SetFloat("DCR_Volume", masterVolume);
            PlayerPrefs.SetFloat("DCR_WalkSpeed", walkSpeed);
            PlayerPrefs.SetFloat("DCR_SnapTurn", snapTurnDegrees);
            PlayerPrefs.SetInt("DCR_InvertY", invertY ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = masterVolume;
        }

        private static string GetStepRationale(string heading)
        {
            switch (heading)
            {
                case "Capturar métricas": return "Las métricas permiten localizar el cuello de botella sin interrumpir la base de datos ni destruir evidencias.";
                case "Inspeccionar sesiones": return "Las sesiones bloqueadas y las consultas lentas ayudan a aislar la causa antes de cambiar la infraestructura.";
                case "Establecer línea base": return "Una prueba de solo lectura confirma el estado inicial sin modificar datos de producción.";
                case "Identificar la bahía": return "Confirmar la bahía evita retirar por error el disco sano y perder la redundancia del RAID 1.";
                case "Aislar el componente": return "El modo mantenimiento y el aislamiento controlado reducen el riesgo de retirar una unidad durante escrituras.";
                case "Reemplazar y reconstruir": return "La reconstrucción del espejo restablece la redundancia; interrumpirla o formatear el arreglo comprometería los datos.";
                case "Verificar temperatura": return "Comparar entrada, salida y RPM permite distinguir una restricción de flujo de un fallo térmico.";
                case "Cambiar filtro": return "Aislar el módulo antes de intervenir evita contacto con partes en movimiento y mantiene el flujo controlado.";
                case "Validar flujo": return "La verificación posterior confirma que el caudal y la temperatura vuelven a valores estables.";
                case "Inspeccionar UPS": return "Revisar carga, batería y alarmas confirma que existe respaldo antes de tocar la distribución eléctrica.";
                case "Transferir carga": return "La línea alternativa debe soportar la carga antes de intervenir la PDU para no dejar el rack sin energía.";
                case "Restaurar circuito": return "El voltaje estable y las protecciones intactas confirman una recuperación eléctrica segura.";
                case "Comprobar replicación": return "La réplica sincronizada permite recuperar el servicio sin dejar datos pendientes o inconsistentes.";
                case "Prueba de integridad": return "La integridad y una consulta de respuesta comprueban que el servicio no solo está encendido, sino que funciona.";
                case "Cerrar incidencia": return "El ticket debe conservar evidencias y resultados para que otra persona pueda auditar la intervención.";
                case "Seleccionar enlace": return "Identificar el uplink documentado evita interrumpir un puerto ajeno o una ruta redundante.";
                case "Leer contadores": return "Los CRC, descartes y flaps ofrecen evidencia de calidad del enlace; borrar contadores elimina información útil.";
                case "Validar la causa": return "Inspeccionar el latiguillo y comparar tendencias permite corregir la causa sin desconectar ambos enlaces.";
                case "Comprobar pulsera": return "La continuidad y la conexión equipotencial reducen el riesgo de descarga electrostática sobre los circuitos.";
                case "Preparar repuesto": return "La superficie ESD y el embalaje protegen el componente hasta el momento de instalarlo.";
                case "Manipular componente": return "Sujetar por los bordes evita contaminar o dañar contactos sensibles.";
                case "Preparar instrumento": return "Comprobar el instrumento y sus unidades hace que la medición sea reproducible y comparable.";
                case "Medir entradas y salidas": return "Medir en puntos definidos permite detectar diferencias reales sin obstruir la ventilación.";
                case "Comparar tendencia": return "La comparación con la línea base evita aceptar una lectura aislada que oculte un problema persistente.";
                case "Inspeccionar estado": return "Registrar carga, batería y alarmas conserva el estado del UPS sin introducir cambios innecesarios.";
                case "Confirmar redundancia": return "La fuente alternativa debe estar disponible antes de probar o modificar la distribución.";
                case "Registrar resultado": return "Guardar lectura, hora y estado crea evidencia verificable de la prueba preventiva.";
                case "Identificar activo": return "Vincular rack, servidor y componente evita asignar el cambio al equipo equivocado.";
                case "Adjuntar evidencias": return "Las mediciones y el repuesto utilizado permiten reconstruir qué se hizo y comprobar el resultado.";
                case "Cerrar intervención": return "El cierre responsable deja constancia del impacto, la validación y quién realizó el trabajo.";
                default: return "Elige el procedimiento que conserva la seguridad, la redundancia y la evidencia del servicio.";
            }
        }

        private void BuildMissionDefinitions()
        {
            missions = new[]
            {
                new MissionDefinition(
                    "DIAGNÓSTICO DE BASE DE DATOS",
                    "El NOC reporta latencia elevada en el servicio de base de datos. Investiga antes de reiniciar producción.",
                    new ProcedureStep("Capturar métricas", "Revisa CPU, memoria, I/O y latencia de consultas.", "Consultar métricas de CPU, RAM, I/O y latencia", "Reiniciar todos los servidores inmediatamente", "Borrar los registros para liberar espacio"),
                    new ProcedureStep("Inspeccionar sesiones", "Busca sesiones bloqueadas y consultas lentas.", "Revisar sesiones activas y consultas de mayor duración", "Deshabilitar el firewall del centro de datos", "Desconectar el switch principal"),
                    new ProcedureStep("Establecer línea base", "Ejecuta una prueba de solo lectura y registra el resultado.", "Ejecutar una consulta de diagnóstico de solo lectura", "Eliminar y volver a crear la base de datos", "Cambiar el esquema en producción sin respaldo")
                ),
                new MissionDefinition(
                    "SUSTITUCIÓN DE UNIDAD SSD",
                    "El nodo DB-03 informa una unidad degradada en el arreglo RAID 1. Hay un repuesto disponible en el kit.",
                    new ProcedureStep("Identificar la bahía", "Confirma qué unidad presenta fallos antes de tocar el rack.", "Verificar el indicador y confirmar la bahía B2", "Retirar la primera unidad que encuentres", "Desconectar el rack completo"),
                    new ProcedureStep("Aislar el componente", "Evita retirar una unidad activa sin preparar el nodo.", "Poner el nodo en mantenimiento y aislar la unidad B2", "Extraer el SSD mientras escribe datos", "Desactivar la redundancia del arreglo"),
                    new ProcedureStep("Reemplazar y reconstruir", "Instala el repuesto y verifica la reconstrucción del espejo.", "Instalar el SSD de repuesto y reconstruir el RAID 1", "Formatear todas las unidades del arreglo", "Reiniciar el servidor durante la reconstrucción")
                ),
                new MissionDefinition(
                    "RECUPERACIÓN DE REFRIGERACIÓN",
                    "El sensor de entrada marca temperatura alta y el caudal de aire está por debajo del umbral.",
                    new ProcedureStep("Verificar temperatura", "Comprueba la lectura de entrada, salida y RPM de los ventiladores.", "Medir temperatura de entrada y RPM de ventiladores", "Ignorar la alarma porque el servidor sigue encendido", "Aumentar el voltaje de la PDU"),
                    new ProcedureStep("Cambiar filtro", "Aísla el módulo antes de retirar el filtro obstruido.", "Aislar el módulo y sustituir el filtro de aire", "Retirar el filtro con el ventilador funcionando", "Bloquear todas las rejillas de ventilación"),
                    new ProcedureStep("Validar flujo", "Restaura el módulo y confirma que la temperatura se estabiliza.", "Restaurar el ventilador y verificar el flujo de aire", "Apagar los sensores térmicos", "Cerrar el ticket sin comprobar temperaturas")
                ),
                new MissionDefinition(
                    "REVISIÓN DE UPS Y DISTRIBUCIÓN ELÉCTRICA",
                    "El panel PDU registró una anomalía. Verifica la alimentación redundante antes de restablecer el circuito.",
                    new ProcedureStep("Inspeccionar UPS", "Comprueba carga, batería y estado de la alimentación redundante.", "Verificar carga, batería y alimentación redundante del UPS", "Desconectar ambas fuentes de alimentación", "Restablecer el breaker sin inspección"),
                    new ProcedureStep("Transferir carga", "Confirma que la línea alternativa soporta la carga.", "Confirmar la línea redundante antes de intervenir la PDU", "Conectar una extensión doméstica al rack", "Apagar el UPS de respaldo"),
                    new ProcedureStep("Restaurar circuito", "Restablece el circuito y valida una salida estable.", "Restablecer el breaker y verificar voltaje estable", "Puentea la protección térmica", "Desconectar la toma de tierra")
                ),
                new MissionDefinition(
                    "VALIDACIÓN Y CIERRE DEL SERVICIO",
                    "Los componentes están recuperados. Valida replicación, integridad y disponibilidad antes de cerrar la intervención.",
                    new ProcedureStep("Comprobar replicación", "Verifica que la réplica esté sincronizada y sin retraso anómalo.", "Comprobar estado de replicación y retraso de réplica", "Eliminar la réplica secundaria", "Desactivar los registros de transacciones"),
                    new ProcedureStep("Prueba de integridad", "Ejecuta las comprobaciones y una consulta de respuesta.", "Ejecutar prueba de integridad y consulta de respuesta", "Borrar los datos de prueba del cliente", "Deshabilitar las copias de seguridad"),
                    new ProcedureStep("Cerrar incidencia", "Adjunta métricas, repuestos utilizados y resultado final.", "Adjuntar evidencias y cerrar el ticket de mantenimiento", "Cerrar el ticket sin guardar resultados", "Eliminar el historial de la intervención")
                )
            };
        }

        private void BuildSideTaskDefinitions()
        {
            sideTasks = new[]
            {
                new SideTaskDefinition(
                    "AUDITORÍA DE PUERTOS DEL SWITCH", ToolNames[0],
                    new ProcedureStep("Seleccionar enlace", "Identifica el uplink asociado al servidor DB-03 antes de iniciar el análisis.",
                        "Seleccionar el puerto uplink documentado para DB-03", "Deshabilitar todos los puertos del switch", "Elegir un puerto libre al azar"),
                    new ProcedureStep("Leer contadores", "Revisa errores CRC, descartes y cambios de estado del enlace.",
                        "Registrar CRC, descartes y flaps sin borrar los contadores", "Borrar contadores antes de guardar la evidencia", "Reiniciar el switch para limpiar las alarmas"),
                    new ProcedureStep("Validar la causa", "Compara los contadores con el historial del enlace y conserva la redundancia.",
                        "Inspeccionar el latiguillo y documentar la tendencia manteniendo el enlace redundante", "Desconectar ambos enlaces para probarlos", "Forzar la velocidad del puerto sin comprobar el extremo opuesto")),
                new SideTaskDefinition(
                    "COMPROBACIÓN DE PROTECCIÓN ESD", ToolNames[1],
                    new ProcedureStep("Comprobar pulsera", "Valida la pulsera antiestática y su conexión al punto equipotencial.",
                        "Confirmar continuidad de la pulsera y conexión a tierra", "Retirar la pulsera para trabajar con libertad", "Conectar la pulsera a una superficie pintada"),
                    new ProcedureStep("Preparar repuesto", "Prepara una superficie de trabajo protegida antes de abrir el embalaje.",
                        "Colocar el repuesto sobre una superficie antiestática y mantenerlo en su bolsa hasta usarlo", "Apoyar el SSD sobre la carcasa del servidor", "Abrir varios repuestos y dejarlos sobre el suelo"),
                    new ProcedureStep("Manipular componente", "La manipulación debe evitar contactos y descargas sobre los circuitos.",
                        "Sujetar el módulo por los bordes y evitar tocar contactos", "Tocar los contactos para comprobar si están limpios", "Deslizar el módulo sobre una tela común")),
                new SideTaskDefinition(
                    "MEDICIÓN DE FLUJO DE AIRE", ToolNames[2],
                    new ProcedureStep("Preparar instrumento", "Comprueba que el anemómetro esté operativo y selecciona la unidad correcta.",
                        "Verificar el instrumento y seleccionar metros por segundo", "Usar una lectura anterior sin revisar el instrumento", "Cambiar la unidad hasta obtener un valor más alto"),
                    new ProcedureStep("Medir entradas y salidas", "Toma lecturas en los puntos definidos sin bloquear las rejillas.",
                        "Medir entrada y salida en los puntos de referencia", "Cubrir la entrada de aire para alterar la lectura", "Medir directamente frente al ventilador con la rejilla bloqueada"),
                    new ProcedureStep("Comparar tendencia", "Compara el resultado con la línea base y confirma que la tendencia se estabiliza.",
                        "Comparar ambas lecturas con la línea base y registrar la estabilización", "Descartar la lectura más baja", "Desactivar los sensores térmicos para cerrar la alarma")),
                new SideTaskDefinition(
                    "VERIFICACIÓN DE RESPALDO UPS", ToolNames[3],
                    new ProcedureStep("Inspeccionar estado", "Consulta carga, batería, alarmas y estado del bypass desde el panel.",
                        "Registrar carga, batería y alarmas sin modificar el bypass", "Activar bypass sin revisar la carga", "Desconectar ambas fuentes para probar autonomía"),
                    new ProcedureStep("Confirmar redundancia", "Comprueba que la línea alternativa esté disponible antes de cualquier prueba.",
                        "Verificar que la alimentación redundante está estable y disponible", "Apagar la línea redundante para evitar reparto", "Puentea las protecciones de la PDU"),
                    new ProcedureStep("Registrar resultado", "Documenta la lectura simulada y deja la configuración en estado normal.",
                        "Guardar lectura, hora y estado normal del UPS en el ticket", "Cambiar umbrales para ocultar la alarma", "Cerrar el ticket sin guardar valores")),
                new SideTaskDefinition(
                    "DOCUMENTACIÓN DEL CAMBIO", ToolNames[4],
                    new ProcedureStep("Identificar activo", "Vincula el ticket con el rack, servidor y componente intervenido.",
                        "Registrar activo DB-03, ubicación y número de parte del componente", "Registrar solo el nombre del operador", "Crear un ticket sin relación con la incidencia original"),
                    new ProcedureStep("Adjuntar evidencias", "Incluye resultados de medición, repuesto y pruebas realizadas.",
                        "Adjuntar lecturas, repuesto utilizado y resultado de validación", "Guardar una captura sin fecha ni identificación", "Eliminar las evidencias anteriores para reducir el tamaño"),
                    new ProcedureStep("Cerrar intervención", "El cierre requiere estado del servicio, hora y responsable.",
                        "Documentar impacto, validación, responsable y hora antes de cerrar", "Cerrar el ticket aunque la réplica siga pendiente", "Eliminar el historial para dejar el panel limpio"))
            };
        }

        private void CreateStations()
        {
            // Positions are aligned with the existing room: operator desk, server aisle, HVAC wall and PDU panel.
            stations[0] = CreateStation(0, "NOC / DIAGNÓSTICO", new Vector3(-18.1f, 0.58f, -4.15f));
            stations[1] = CreateStation(1, "RACK DB-03 / SSD", new Vector3(-12.15f, 0.58f, -5.75f));
            stations[2] = CreateStation(2, "HVAC / REFRIGERACIÓN", new Vector3(-6.35f, 0.58f, -11.55f));
            stations[3] = CreateStation(3, "PDU / UPS", new Vector3(-3.35f, 0.58f, -8.55f));
            SetStationStates();
        }

        private void CreateMaintenanceActions()
        {
            if (stations == null || stations.Length < 4 || sideTasks == null) return;

            maintenanceActions[0] = CreateMaintenanceAction(0, stations[0], new Vector3(0.94f, 0f, 0.52f));
            maintenanceActions[1] = CreateMaintenanceAction(1, stations[1], new Vector3(0.94f, 0f, 0.52f));
            maintenanceActions[2] = CreateMaintenanceAction(2, stations[2], new Vector3(0.94f, 0f, 0.52f));
            maintenanceActions[3] = CreateMaintenanceAction(3, stations[3], new Vector3(0.94f, 0f, 0.52f));
            // The documentation terminal sits beside the NOC console, using the same generated geometry.
            maintenanceActions[4] = CreateMaintenanceAction(4, stations[0], new Vector3(-0.94f, 0f, 0.52f));
        }

        private DCRMaintenanceAction CreateMaintenanceAction(int index, DCRStation anchor, Vector3 localOffset)
        {
            if (anchor == null || index < 0 || index >= sideTasks.Length) return null;

            GameObject root = new GameObject("DCR_OptionalTask_" + index);
            root.transform.SetPositionAndRotation(anchor.transform.TransformPoint(localOffset), anchor.transform.rotation);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.55f, 1.18f, 0.34f);
            collider.center = new Vector3(0f, 0.25f, 0f);

            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "ServiceTerminalHousing";
            housing.transform.SetParent(root.transform, false);
            housing.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            housing.transform.localScale = new Vector3(0.52f, 0.72f, 0.30f);
            Collider housingCollider = housing.GetComponent<Collider>();
            if (housingCollider != null) housingCollider.enabled = false;
            Renderer housingRenderer = housing.GetComponent<Renderer>();
            housingRenderer.material = CreateMaterial(new Color(0.06f, 0.20f, 0.27f, 1f));

            GameObject display = GameObject.CreatePrimitive(PrimitiveType.Cube);
            display.name = "TaskDisplay";
            display.transform.SetParent(root.transform, false);
            display.transform.localPosition = new Vector3(0f, 0.22f, 0.17f);
            display.transform.localScale = new Vector3(0.40f, 0.25f, 0.035f);
            Collider displayCollider = display.GetComponent<Collider>();
            if (displayCollider != null) displayCollider.enabled = false;
            display.GetComponent<Renderer>().material = CreateMaterial(new Color(0.025f, 0.12f, 0.17f, 1f));

            Renderer[] stepIndicators = new Renderer[3];
            for (int i = 0; i < stepIndicators.Length; i++)
            {
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = "TaskProgressSegment_" + (i + 1);
                segment.transform.SetParent(root.transform, false);
                segment.transform.localPosition = new Vector3((i - 1) * 0.12f, 0.22f, 0.194f);
                segment.transform.localScale = new Vector3(0.075f, 0.045f, 0.012f);
                Collider segmentCollider = segment.GetComponent<Collider>();
                if (segmentCollider != null) segmentCollider.enabled = false;
                stepIndicators[i] = segment.GetComponent<Renderer>();
                stepIndicators[i].material = CreateMaterial(new Color(0.10f, 0.17f, 0.20f, 1f));
            }

            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "TaskStatusLamp";
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 0.52f, 0.02f);
            lamp.transform.localScale = Vector3.one * 0.105f;
            Collider lampCollider = lamp.GetComponent<Collider>();
            if (lampCollider != null) lampCollider.enabled = false;
            lamp.GetComponent<Renderer>().material = CreateMaterial(new Color(0.08f, 0.70f, 0.85f, 1f));

            DCRMaintenanceAction action = root.AddComponent<DCRMaintenanceAction>();
            action.Configure(index, sideTasks[index].Title, housingRenderer, stepIndicators);
            return action;
        }



        private DCRStation CreateStation(int missionIndex, string label, Vector3 position)
        {
            GameObject root = new GameObject("DCR_ServiceStation_" + missionIndex);
            root.transform.position = position;
            Vector3 look = new Vector3(-20.3f, position.y, -5.3f) - position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            BoxCollider rootCollider = root.AddComponent<BoxCollider>();
            rootCollider.size = new Vector3(0.64f, 1.72f, 0.32f);
            rootCollider.center = new Vector3(0f, 0.28f, 0f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ConsoleHousing";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.64f, 0.98f, 0.28f);
            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null) bodyCollider.enabled = false;
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.material = CreateMaterial(new Color(0.045f, 0.16f, 0.22f, 1f));

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "StatusScreen";
            screen.transform.SetParent(root.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.14f, 0.17f);
            screen.transform.localScale = new Vector3(0.52f, 0.42f, 0.045f);
            Collider screenCollider = screen.GetComponent<Collider>();
            if (screenCollider != null) screenCollider.enabled = false;
            Renderer screenRenderer = screen.GetComponent<Renderer>();
            screenRenderer.material = CreateMaterial(new Color(0.035f, 0.09f, 0.12f, 1f));

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "WaypointBeacon";
            indicator.transform.SetParent(root.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            indicator.transform.localScale = Vector3.one * 0.19f;
            Collider beaconCollider = indicator.GetComponent<Collider>();
            if (beaconCollider != null) beaconCollider.enabled = false;
            Renderer beaconRenderer = indicator.GetComponent<Renderer>();
            beaconRenderer.material = CreateMaterial(new Color(0.08f, 0.7f, 0.85f, 1f));

            DCRStation station = root.AddComponent<DCRStation>();
            station.Configure(missionIndex, label, bodyRenderer, screenRenderer, beaconRenderer);
            return station;
        }

        private IEnumerator ValidateRuntimeTextResourcesNextFrame()
        {
            // Let Unity finish the first Canvas rebuild before checking the actual
            // font texture and CanvasRenderer material used by the initial menu.
            yield return null;
            Canvas.ForceUpdateCanvases();
            ValidateRuntimeTextResources();
        }

        private void ValidateRuntimeTextResources()
        {
            Canvas.ForceUpdateCanvases();
            Font font = DCRUIStyle.GetRuntimeFont();
            Material textMaterial = DCRUIStyle.GetRuntimeTextMaterial();
            Shader uiShader = textMaterial != null ? textMaterial.shader : null;
            if (font == null)
                Debug.LogError("DCR_UI_FATAL: no se pudo cargar DCRLiberationSans.ttf ni una fuente integrada.");
            if (textMaterial == null || uiShader == null || !uiShader.isSupported)
                Debug.LogError("DCR_UI_FATAL: el material predeterminado de Canvas/UGUI no está disponible.");

            Text[] texts = overlayCanvas != null ? overlayCanvas.GetComponentsInChildren<Text>(true) : Array.Empty<Text>();
            int visibleTexts = 0;
            int failedTexts = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) continue;
                Canvas canvas = text.GetComponentInParent<Canvas>();
                Vector2 extents = text.rectTransform != null ? text.rectTransform.rect.size : Vector2.zero;
                CanvasRenderer renderer = text.canvasRenderer;
                Material rendererMaterial = renderer != null ? renderer.GetMaterial() : null;
                bool canvasModeValid = canvas != null && canvas.enabled &&
                    (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                     (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null &&
                      (canvas.worldCamera == playerCamera || canvas.worldCamera == xrOverlayCamera)));
                bool valid = text.isActiveAndEnabled && canvasModeValid && text.font != null &&
                    text.materialForRendering != null && text.materialForRendering.shader == uiShader &&
                    rendererMaterial != null && rendererMaterial.shader == uiShader &&
                    renderer.materialCount > 0 && extents.x >= 16f && extents.y >= 12f;
                int generatedCharacters = 0;
                if (valid)
                {
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                    TextGenerator generator = text.cachedTextGenerator;
                    bool generated = generator.Populate(text.text, text.GetGenerationSettings(extents));
                    generatedCharacters = generator.characterCountVisible;
                    valid = generated && generatedCharacters > 0 && text.mainTexture != null;
                }
                if (valid) visibleTexts++;
                else
                {
                    failedTexts++;
                    Debug.LogError("DCR_UI_TEXT_FAILED: " + text.name + " | canvas=" +
                        (canvas != null ? canvas.renderMode.ToString() : "MISSING") +
                        " | font=" + (text.font != null ? text.font.name : "MISSING") +
                        " | rect=" + extents + " | generated=" + generatedCharacters +
                        " | shader=" + (rendererMaterial != null && rendererMaterial.shader != null ? rendererMaterial.shader.name : "MISSING"));
                }
            }

            bool xrStackValid = playerController == null || !playerController.IsXRActive ||
                (xrOverlayCamera != null && baseCameraData != null && baseCameraData.cameraStack != null &&
                 baseCameraData.cameraStack.Contains(xrOverlayCamera) && xrOverlayCamera.cullingMask == (1 << DCRUIStyle.UILayer) &&
                 xrOverlayCamera.GetUniversalAdditionalCameraData().clearDepth);
            Graphic[] graphics = overlayCanvas != null ? overlayCanvas.GetComponentsInChildren<Graphic>(true) : Array.Empty<Graphic>();
            int wrongLayerGraphics = 0;
            for (int i = 0; i < graphics.Length; i++)
                if (graphics[i] != null && graphics[i].gameObject.layer != DCRUIStyle.UILayer) wrongLayerGraphics++;

            Debug.Log("DCR_UI_DIAGNOSTIC: renderMode=" + (overlayCanvas != null ? overlayCanvas.renderMode.ToString() : "MISSING") +
                " | sortingOrder=" + (overlayCanvas != null ? overlayCanvas.sortingOrder.ToString() : "n/a") +
                " | renderCamera=" + (overlayCanvas != null && overlayCanvas.worldCamera != null ? overlayCanvas.worldCamera.name : "OVERLAY") +
                " | xrStack=" + xrStackValid + " | uiLayerGraphics=" + graphics.Length + " | wrongLayerGraphics=" + wrongLayerGraphics +
                " | visibleTexts=" + visibleTexts + " | failedTexts=" + failedTexts);
            if (overlayCanvas == null || failedTexts > 0 || visibleTexts < 5 || !xrStackValid || wrongLayerGraphics > 0)
                Debug.LogError("DCR_UI_FATAL: la interfaz frontal no completó las validaciones de Canvas, capa UI, texto o cámara XR.");
        }



        private void CreateOverlayCanvas()
        {
            bool xrActive = playerController != null && playerController.IsXRActive;
            if (xrActive)
            {
                uiRenderCamera = CreateXROverlayCamera();
                if (uiRenderCamera == null)
                {
                    // The fallback keeps menus interactive, but it is deliberately reported as
                    // degraded: only the URP Overlay camera stack guarantees no scene-depth occlusion.
                    uiRenderCamera = playerCamera;
                    Debug.LogError("DCR_UI_XR_FALLBACK: el renderer activo no permite apilar una cámara URP Overlay. La UI usa temporalmente la cámara principal y podría ser tapada; cambia el renderer a Universal Renderer para conservar la interfaz frontal garantizada.");
                }
            }

            overlayCanvas = DCRUIStyle.CreateScreenCanvas(xrActive ? uiRenderCamera : null, xrActive);
            overlayRaycaster = overlayCanvas != null ? overlayCanvas.GetComponent<GraphicRaycaster>() : null;

            uiEventSystem = EventSystem.current;
            if (uiEventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("DCR_UIEventSystem", typeof(EventSystem));
                uiEventSystem = eventSystemObject.GetComponent<EventSystem>();
            }
            if (uiEventSystem != null)
                uiPointerData = new PointerEventData(uiEventSystem) { button = PointerEventData.InputButton.Left };

            lastCanvasXRMode = xrActive;
        }

        private Camera CreateXROverlayCamera()
        {
            if (playerCamera == null) return null;

            baseCameraData = playerCamera.GetUniversalAdditionalCameraData();
            baseCameraData.renderType = CameraRenderType.Base;
            List<Camera> stack = baseCameraData.cameraStack;
            if (stack == null)
            {
                Debug.LogError("DCR_UI_XR_STACK_FAILED: el renderer URP activo no admite cámara Overlay.");
                return null;
            }

            if (xrOverlayCamera == null)
            {
                GameObject cameraObject = new GameObject("DCR_XR_UI_OverlayCamera", typeof(Camera));
                cameraObject.transform.SetParent(playerCamera.transform, false);
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;
                cameraObject.transform.localScale = Vector3.one;

                xrOverlayCamera = cameraObject.GetComponent<Camera>();
                xrOverlayCamera.CopyFrom(playerCamera);
                xrOverlayCamera.transform.localPosition = Vector3.zero;
                xrOverlayCamera.transform.localRotation = Quaternion.identity;
                xrOverlayCamera.transform.localScale = Vector3.one;
                xrOverlayCamera.targetTexture = null;
                xrOverlayCamera.cullingMask = 1 << DCRUIStyle.UILayer;
                xrOverlayCamera.clearFlags = CameraClearFlags.Nothing;
                xrOverlayCamera.nearClipPlane = 0.01f;
                xrOverlayCamera.farClipPlane = 10f;
                xrOverlayCamera.useOcclusionCulling = false;
                xrOverlayCamera.stereoTargetEye = StereoTargetEyeMask.Both;
                xrOverlayCamera.depth = playerCamera.depth + 1f;
                xrOverlayCamera.enabled = true;

                UniversalAdditionalCameraData overlayData = xrOverlayCamera.GetUniversalAdditionalCameraData();
                overlayData.renderType = CameraRenderType.Overlay;
                overlayData.allowXRRendering = true;
                overlayData.renderPostProcessing = false;
                overlayData.renderShadows = false;
                overlayData.requiresColorOption = CameraOverrideOption.Off;
                overlayData.requiresDepthOption = CameraOverrideOption.Off;
                overlayData.volumeLayerMask = 0;
                if (!overlayData.clearDepth)
                {
                    Debug.LogError("DCR_UI_XR_DEPTH_FAILED: la cámara Overlay no limpia profundidad y podría quedar oculta tras la escena.");
                    Destroy(xrOverlayCamera.gameObject);
                    xrOverlayCamera = null;
                    return null;
                }
            }

            stack = baseCameraData.cameraStack;
            if (stack == null) return null;
            if (!stack.Contains(xrOverlayCamera)) stack.Add(xrOverlayCamera);
            return xrOverlayCamera;
        }

        private void RemoveXROverlayCamera()
        {
            if (xrOverlayCamera == null) return;
            if (baseCameraData != null)
            {
                List<Camera> stack = baseCameraData.cameraStack;
                if (stack != null) stack.Remove(xrOverlayCamera);
            }
            Destroy(xrOverlayCamera.gameObject);
            xrOverlayCamera = null;
            uiRenderCamera = null;
            baseCameraData = null;
        }

        private RectTransform CreateUIRect(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position, Color color,
            bool raycastTarget = false)
        {
            return DCRUIStyle.CreateImage(parent, name, anchorMin, anchorMax, pivot,
                size, position, color, raycastTarget);
        }

        private Text CreateUIText(Transform parent, string name, string value, DCRTextRole role,
            Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            return DCRUIStyle.CreateText(parent, name, value, role, color, alignment,
                anchorMin, anchorMax, offsetMin, offsetMax);
        }

        private void CreateHUD()
        {
            if (overlayCanvas == null) return;
            hudRoot = CreateUIRect(overlayCanvas.transform, "DCR_HUD_Panel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(650f, 188f), new Vector2(24f, -24f), new Color(0.006f, 0.022f, 0.032f, 0.92f)).gameObject;
            hudText = CreateUIText(hudRoot.transform, "HUD_Text", "DATA CENTER RESCUE", DCRTextRole.Hud,
                new Color(0.82f, 0.94f, 1f, 1f), TextAnchor.UpperLeft,
                Vector2.zero, Vector2.one, new Vector2(18f, 12f), new Vector2(-18f, -12f));

            promptRoot = CreateUIRect(overlayCanvas.transform, "DCR_InteractionPrompt",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(860f, 100f), new Vector2(0f, 28f), new Color(0.006f, 0.022f, 0.032f, 0.94f)).gameObject;
            promptText = CreateUIText(promptRoot.transform, "InteractionPromptText", string.Empty,
                DCRTextRole.Prompt, new Color(0.82f, 0.96f, 1f, 1f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(16f, 8f), new Vector2(-16f, -8f));

            objectiveMarkerRoot = CreateUIRect(overlayCanvas.transform, "DCR_ObjectiveBanner",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(650f, 64f), new Vector2(24f, -222f), new Color(0.008f, 0.035f, 0.05f, 0.94f));
            objectiveMarkerText = CreateUIText(objectiveMarkerRoot, "ObjectiveMarkerText", "OBJETIVO",
                DCRTextRole.WorldLabel, new Color(0.78f, 0.97f, 1f, 1f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(12f, 5f), new Vector2(-12f, -5f));

            RectTransform crosshairRect = CreateUIRect(overlayCanvas.transform, "DCR_Crosshair",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(56f, 56f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
            crosshairText = CreateUIText(crosshairRect, "CenterReticleText", "+", DCRTextRole.Reticle,
                new Color(0.55f, 0.92f, 1f, 0.95f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            xrMenuCursorRoot = CreateUIRect(overlayCanvas.transform, "DCR_XR_MenuCursor",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(18f, 18f), Vector2.zero, new Color(0.20f, 0.92f, 1f, 0.95f));

            hudRoot.SetActive(false);
            promptRoot.SetActive(false);
            objectiveMarkerRoot.gameObject.SetActive(false);
            crosshairRect.gameObject.SetActive(false);
            xrMenuCursorRoot.gameObject.SetActive(false);
        }



        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
                throw new InvalidOperationException("Data Center Rescue: no se encontró un shader compatible para la interfaz.");

            Material material = new Material(shader);
            runtimeMaterials.Add(material);
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.18f);
            }
            return material;
        }

        private void Update()
        {
            if (!initialized) return;
            menuNavigationActivated = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                HandleEscape();
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame && mode == ScreenMode.Gameplay)
                ShowPlayerGuide(ScreenMode.Gameplay);

            UpdateXRPauseInput();
            UpdateBodyScrollInput();
            UpdateMenuKeyboardNavigation();
            UpdateToolSelection();
            UpdateResponsiveUI();

            if (mode == ScreenMode.Gameplay || mode == ScreenMode.Procedure || mode == ScreenMode.SideTask)
                elapsedSeconds += Time.deltaTime;

            UpdateInteraction();
            UpdateHUD();
        }

        private void UpdateXRPauseInput()
        {
            if (playerController == null || !playerController.IsXRActive)
            {
                lastXRMenuButton = false;
                return;
            }

            bool pressed = false;
            XRInputDevice leftHand = playerController.LeftHand;
            if (leftHand.isValid)
            {
                bool menuButton = false;
                bool primaryButton = false;
                bool hasMenuButton = leftHand.TryGetFeatureValue(XRCommonUsages.menuButton, out menuButton);
                bool hasPrimaryButton = leftHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out primaryButton);
                pressed = (hasMenuButton && menuButton) || (hasPrimaryButton && primaryButton);
            }

            if (pressed && !lastXRMenuButton && mode == ScreenMode.Gameplay)
                HandleEscape();

            lastXRMenuButton = pressed;
        }

        private void UpdateBodyScrollInput()
        {
            if (bodyScrollRect == null || !bodyScrollRect.isActiveAndEnabled) return;
            float delta = 0f;
            Keyboard keyboard = Keyboard.current;
            if (playerController != null && playerController.IsXRActive)
            {
                XRInputDevice rightHand = playerController.RightHand;
                if (rightHand.isValid && rightHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 axis) &&
                    Mathf.Abs(axis.y) > 0.2f)
                    delta = axis.y * Time.unscaledDeltaTime * 0.75f;
            }
            else
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > 0.1f) delta = wheel / 120f * 0.12f;
                }
                if (keyboard != null && keyboard.pageDownKey.wasPressedThisFrame) delta = -0.35f;
                else if (keyboard != null && keyboard.pageUpKey.wasPressedThisFrame) delta = 0.35f;
            }

            if (Mathf.Abs(delta) > 0.0001f)
            {
                bodyScrollRect.StopMovement();
                bodyScrollRect.verticalNormalizedPosition = Mathf.Clamp01(bodyScrollRect.verticalNormalizedPosition + delta);
            }
        }

        private void UpdateMenuKeyboardNavigation()
        {
            if (mode == ScreenMode.Gameplay || panelButtons.Count == 0 ||
                (playerController != null && playerController.IsXRActive))
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            int next = keyboardMenuSelection;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                next = (next + 1) % panelButtons.Count;
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                next = (next - 1 + panelButtons.Count) % panelButtons.Count;

            if (next != keyboardMenuSelection)
            {
                keyboardMenuSelection = next;
                RefreshKeyboardMenuSelection();
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame)
            {
                menuNavigationActivated = true;
                OnButtonPressed(keyboardMenuSelection);
            }
        }

        private void RefreshKeyboardMenuSelection()
        {
            for (int i = 0; i < panelButtons.Count; i++)
            {
                if (panelButtons[i] != null)
                    panelButtons[i].SetSelected(i == keyboardMenuSelection);
            }
        }

        private void UpdateToolSelection()
        {
            if (mode != ScreenMode.Gameplay) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectTool(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) SelectTool(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) SelectTool(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) SelectTool(3);
                else if (keyboard.digit5Key.wasPressedThisFrame) SelectTool(4);
            }

            if (playerController == null || !playerController.IsXRActive)
            {
                lastXRToolCycle = false;
                return;
            }

            bool cyclePressed = false;
            XRInputDevice leftHand = playerController.LeftHand;
            if (leftHand.isValid)
                leftHand.TryGetFeatureValue(XRCommonUsages.secondaryButton, out cyclePressed);

            if (cyclePressed && !lastXRToolCycle)
                SelectTool((selectedToolIndex + 1) % ToolNames.Length);
            lastXRToolCycle = cyclePressed;
        }

        private void SelectTool(int index)
        {
            if (index < 0 || index >= ToolNames.Length || selectedToolIndex == index) return;
            selectedToolIndex = index;
            feedback = "HERRAMIENTA EQUIPADA: " + ToolNames[selectedToolIndex];
            feedbackUntil = Time.time + 2.0f;
            PlayFeedback(clickTone);
        }

        private void UpdateResponsiveUI()
        {
            if (overlayCanvas == null || playerCamera == null) return;
            bool xrActive = playerController != null && playerController.IsXRActive;
            if (xrActive != lastCanvasXRMode || lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
            {
                if (xrActive)
                {
                    uiRenderCamera = CreateXROverlayCamera();
                    if (uiRenderCamera == null) uiRenderCamera = playerCamera;
                }
                else
                {
                    RemoveXROverlayCamera();
                }

                DCRUIStyle.ConfigureScreenCanvas(overlayCanvas,
                    xrActive ? uiRenderCamera : null, xrActive);
                lastCanvasXRMode = xrActive;
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                Canvas.ForceUpdateCanvases();
            }
        }



        private void UpdateInteraction()
        {
            if (mode != ScreenMode.Gameplay)
            {
                bool usingXRPointer;
                DCRWorldButton menuButton = GetOverlayButtonUnderPointer(out usingXRPointer);
                if (xrPointer != null) xrPointer.enabled = false;

                if (hoveredButton != menuButton)
                {
                    if (hoveredButton != null) hoveredButton.SetHovered(false);
                    hoveredButton = menuButton;
                    if (hoveredButton != null) hoveredButton.SetHovered(true);
                }
                if (hoveredMaintenanceAction != null)
                {
                    hoveredMaintenanceAction.SetHovered(false);
                    hoveredMaintenanceAction = null;
                }

                if (InteractionPressed() && menuButton != null)
                    OnButtonPressed(menuButton.Index);
                SetPromptText(string.Empty);
                return;
            }

            Ray interactionRay = GetInteractionRay(out bool usingXRWorldPointer);
            if (xrPointer != null)
            {
                xrPointer.enabled = usingXRWorldPointer;
                if (usingXRWorldPointer)
                {
                    xrPointer.SetPosition(0, interactionRay.origin);
                    xrPointer.SetPosition(1, interactionRay.origin + interactionRay.direction * 3.2f);
                }
            }

            DCRStation hitStation = null;
            DCRMaintenanceAction hitMaintenanceAction = null;
            if (Physics.Raycast(interactionRay, out RaycastHit hit, InteractionRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                hitMaintenanceAction = hit.collider.GetComponentInParent<DCRMaintenanceAction>();
                if (hitMaintenanceAction == null)
                    hitStation = hit.collider.GetComponentInParent<DCRStation>();
            }

            if (hoveredButton != null)
            {
                hoveredButton.SetHovered(false);
                hoveredButton = null;
            }
            if (hoveredMaintenanceAction != hitMaintenanceAction)
            {
                if (hoveredMaintenanceAction != null) hoveredMaintenanceAction.SetHovered(false);
                hoveredMaintenanceAction = hitMaintenanceAction;
                if (hoveredMaintenanceAction != null) hoveredMaintenanceAction.SetHovered(true);
            }

            if (InteractionPressed())
            {
                if (hitMaintenanceAction != null)
                    UseMaintenanceAction(hitMaintenanceAction);
                else if (hitStation != null && hitStation == GetCurrentStation())
                    OpenProcedure(hitStation);
            }

            if (promptText != null)
            {
                if (Time.time <= feedbackUntil && !string.IsNullOrEmpty(feedback))
                    SetPromptText(feedback);
                else if (hitMaintenanceAction != null && hitMaintenanceAction.IsCompleted)
                    SetPromptText("AUDITORÍA COMPLETADA · " + GetSideTaskDisplayName(hitMaintenanceAction.ActionIndex));
                else if (hitMaintenanceAction != null && IsValidActionIndex(hitMaintenanceAction.ActionIndex))
                {
                    int actionIndex = hitMaintenanceAction.ActionIndex;
                    SideTaskDefinition task = sideTasks[actionIndex];
                    if (!mainServiceComplete)
                    {
                        DCRStation requiredStation = GetCurrentStation();
                        string requiredStationName = requiredStation != null ? requiredStation.StationName : "OBJETIVO PRINCIPAL";
                        SetPromptText("AUDITORÍA OPCIONAL · DISPONIBLE DESPUÉS DE RESTAURAR DB-03 · CONTINÚA CON " + requiredStationName);
                    }
                    else if (selectedToolIndex == actionIndex)
                        SetPromptText(task.Title + " · PULSA E / GATILLO PARA ABRIR · FASE " + sideTaskProgress[actionIndex] + "/3");
                    else
                        SetPromptText("EQUIPA " + task.RequiredTool + " · TECLA " + (actionIndex + 1) + " Y LUEGO PULSA E");
                }
                else if (hitStation != null && hitStation == GetCurrentStation())
                    SetPromptText(hitStation.StationName + " · PULSA E / GATILLO PARA ABRIR EL PROCEDIMIENTO");
                else if (hitStation != null && !mainServiceComplete)
                    SetPromptText("ESA ESTACIÓN AÚN NO CORRESPONDE · " + GetObjectiveHint());
                else
                    SetPromptText(GetObjectiveHint());
            }
        }

        private Ray GetXRControllerRay()
        {
            if (playerController != null && playerController.RightHand.isValid &&
                playerController.RightHand.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 localPosition) &&
                playerController.RightHand.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion localRotation))
            {
                Vector3 origin = playerRig.TransformPoint(localPosition);
                Vector3 direction = playerRig.TransformDirection(localRotation * Vector3.forward);
                return new Ray(origin, direction.normalized);
            }
            return new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        }

        private DCRWorldButton GetOverlayButtonUnderPointer(out bool usingXRPointer)
        {
            usingXRPointer = playerController != null && playerController.IsXRActive;
            if (overlayRaycaster == null || uiPointerData == null || playerCamera == null)
                return null;

            Vector2 screenPosition;
            if (usingXRPointer)
            {
                Ray ray = GetXRControllerRay();
                Camera projectionCamera = overlayCanvas != null && overlayCanvas.renderMode == RenderMode.ScreenSpaceCamera && overlayCanvas.worldCamera != null
                    ? overlayCanvas.worldCamera
                    : playerCamera;
                Vector3 projected = projectionCamera.WorldToScreenPoint(ray.origin + ray.direction * 4f);
                if (projected.z <= 0f)
                    screenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                else
                    screenPosition = new Vector2(projected.x, projected.y);
                screenPosition.x = Mathf.Clamp(screenPosition.x, 0f, Screen.width);
                screenPosition.y = Mathf.Clamp(screenPosition.y, 0f, Screen.height);

                RectTransform canvasRect = overlayCanvas.transform as RectTransform;
                if (xrMenuCursorRoot != null && canvasRect != null &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition,
                        overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlayCanvas.worldCamera,
                        out Vector2 cursorLocal))
                {
                    xrMenuCursorRoot.anchoredPosition = cursorLocal;
                    xrMenuCursorRoot.gameObject.SetActive(mode != ScreenMode.Gameplay);
                }
            }
            else
            {
                screenPosition = Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (xrMenuCursorRoot != null) xrMenuCursorRoot.gameObject.SetActive(false);
            }

            uiPointerData.position = screenPosition;
            uiRaycastResults.Clear();
            overlayRaycaster.Raycast(uiPointerData, uiRaycastResults);
            for (int i = 0; i < uiRaycastResults.Count; i++)
            {
                DCRWorldButton button = uiRaycastResults[i].gameObject.GetComponentInParent<DCRWorldButton>();
                if (button != null)
                    return button;
            }
            return null;
        }


        private void SetPromptText(string value)
        {
            if (promptText == null) return;
            string safeValue = value ?? string.Empty;
            if (promptText.text != safeValue)
                promptText.text = safeValue;
        }

        private Ray GetInteractionRay(out bool usingXRPointer)
        {
            usingXRPointer = false;
            if (playerController != null && playerController.IsXRActive)
            {
                usingXRPointer = true;
                return GetXRControllerRay();
            }
            return playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }



        private bool InteractionPressed()
        {
            if (menuNavigationActivated) return false;
            bool pressed = false;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (mode == ScreenMode.Gameplay && keyboard != null)
                pressed = keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
            if (mode != ScreenMode.Gameplay && mouse != null && mouse.leftButton.wasPressedThisFrame)
                pressed = true;

            if (playerController != null && playerController.IsXRActive)
            {
                XRInputDevice hand = playerController.RightHand;
                float trigger = 0f;
                if (hand.isValid)
                    hand.TryGetFeatureValue(XRCommonUsages.trigger, out trigger);
                bool current = trigger > 0.72f;
                if (current && !lastXRTrigger) pressed = true;
                lastXRTrigger = current;
            }
            else
            {
                lastXRTrigger = false;
            }
            return pressed;
        }

        private void HandleEscape()
        {
            switch (mode)
            {
                case ScreenMode.Gameplay:
                    ShowPauseMenu();
                    break;
                case ScreenMode.Procedure:
                    ClosePanelToGameplay();
                    activeProcedureStation = null;
                    break;
                case ScreenMode.SideTask:
                    ClosePanelToGameplay();
                    activeSideTaskIndex = -1;
                    break;
                case ScreenMode.PlayerGuide:
                    if (guideReturnMode == ScreenMode.Pause) ShowPauseMenu();
                    else if (guideReturnMode == ScreenMode.Tutorial) ShowTutorial();
                    else if (guideReturnMode == ScreenMode.MainMenu) ShowMainMenu();
                    else ClosePanelToGameplay();
                    break;
                case ScreenMode.OperationsLog:
                case ScreenMode.VersionHistory:
                    RestoreLogReturnPage();
                    break;
                case ScreenMode.Options:
                    RestoreOptionsReturnPage();
                    break;
                case ScreenMode.Controls:
                    ShowMainMenu();
                    break;
                case ScreenMode.Tutorial:
                    ShowMainMenu();
                    break;
                case ScreenMode.Pause:
                    ResumeMission();
                    break;
                case ScreenMode.Results:
                    ShowMainMenu();
                    break;
            }
        }

        private void ShowMainMenu()
        {
            string progress = "\n\nRÉCORD: " + PlayerPrefs.GetInt("DCR_BestScore", 0) +
                              "  ·  TURNOS CERRADOS: " + PlayerPrefs.GetInt("DCR_CompletedShifts", 0);
            BuildPanel(ScreenMode.MainMenu, "DATA CENTER RESCUE",
                "SIMULADOR DE MANTENIMIENTO DE SERVIDORES\n\n" +
                "OBJETIVO: recuperar DB-03 de forma segura y conservar la redundancia.\n" +
                "Sigue el objetivo del HUD, acércate a la consola indicada y pulsa E (PC) o el gatillo derecho (VR).\n" +
                "Lee cada procedimiento y selecciona la acción segura. Después puedes completar cinco auditorías preventivas. Abre GUÍA DEL JUGADOR para conocer las tareas y controles; los textos largos se desplazan con rueda o PAGE UP/DOWN (PC) o stick derecho (VR)." + progress,
                new[] { "INICIAR TURNO", "GUÍA DEL JUGADOR", "BITÁCORA OPERATIVA", "HISTORIAL DE VERSIONES", "CONFIGURACIÓN", "CONTROLES PC / VR", "SALIR DEL JUEGO" });
            if (hudRoot != null) hudRoot.SetActive(false);
        }

        private void ShowTutorial()
        {
            string body = string.Join("\n", new[]
            {
                "TU PRIMER TURNO · OBJETIVO",
                "Recupera el servicio de base de datos DB-03. No necesitas conocer comandos reales: el simulador te muestra el síntoma, explica el procedimiento seguro y te pide elegir la mejor acción.",
                "",
                "ORDEN DE TRABAJO",
                "1. Diagnosticar la base de datos: métricas, sesiones y prueba de solo lectura.",
                "2. Sustituir el SSD degradado: identificar bahía, aislar unidad y reconstruir RAID 1.",
                "3. Recuperar refrigeración: medir temperatura/RPM, cambiar filtro y validar flujo.",
                "4. Revisar UPS/PDU: comprobar respaldo, transferir carga y validar alimentación.",
                "5. Validar el servicio: comprobar replicación, integridad y cerrar el ticket.",
                "",
                "CÓMO COMPLETAR CADA PASO",
                "Sigue el objetivo del HUD, acércate a la consola cian y pulsa E (PC) o el gatillo derecho (VR). Lee SÍNTOMA, QUÉ HACER y POR QUÉ; selecciona una de las tres respuestas. Si fallas, verás una explicación y podrás reintentar con penalización.",
                "",
                "Tras restaurar DB-03 puedes completar cinco auditorías opcionales. Cada una tiene tres fases y una herramienta obligatoria. Abre GUÍA DEL JUGADOR para consultar controles y herramientas."
            });
            BuildPanel(ScreenMode.Tutorial, "BRIEFING · PRIMER TURNO", body,
                new[] { "COMENZAR TURNO", "LEER GUÍA COMPLETA", "VOLVER AL MENÚ" });
        }

        private void ShowPlayerGuide(ScreenMode returnMode)
        {
            guideReturnMode = returnMode;
            string body = string.Join("\n", new[]
            {
                "OBJETIVO",
                "Restaura DB-03 completando cinco incidencias en orden. Después puedes hacer cinco auditorías opcionales para ganar puntos. Es una simulación educativa; no controla servidores reales.",
                "",
                "CICLO DE JUEGO",
                "1. LOCALIZA: mira el objetivo y la distancia en las bandas superiores del HUD. Ve a la consola cian.",
                "2. INTERACTÚA: apunta al panel y pulsa E (PC) o el gatillo derecho (VR).",
                "3. DIAGNOSTICA: lee SÍNTOMA, QUÉ HACER y POR QUÉ antes de responder.",
                "4. DECIDE: elige una de las tres acciones. Si fallas, revisa el aviso y reintenta; pierdes puntos, no la partida.",
                "5. AVANZA: al validar el paso, vuelve a la sala y sigue el siguiente objetivo. La estación activa se señala en el HUD; las consolas que todavía no corresponden no aceptan la intervención.",
                "",
                "INCIDENCIAS PRINCIPALES · 3 PASOS CADA UNA",
                "1 · Diagnóstico de base de datos: métricas, sesiones y consulta de solo lectura.",
                "2 · SSD / RAID 1: identificar bahía, aislar unidad y reconstruir el espejo.",
                "3 · Recuperación de refrigeración: medir temperatura/RPM, cambiar filtro y validar caudal.",
                "4 · UPS / PDU: inspeccionar respaldo, transferir carga y validar voltaje.",
                "5 · Servicio: verificar replicación, integridad y documentar el cierre.",
                "",
                "AUDITORÍAS OPCIONALES · EQUIPA LA HERRAMIENTA",
                "1 · Escáner de red: puertos del switch y enlace de DB-03.",
                "2 · Kit ESD: pulsera, superficie antiestática y manipulación de repuesto.",
                "3 · Anemómetro: flujo de aire y tendencia térmica.",
                "4 · Multímetro: UPS, redundancia y salida eléctrica.",
                "5 · Tablet de servicio: identificar activo, guardar evidencias y cerrar registro.",
                "PC: teclas 1–5. VR: botón secundario del mando izquierdo. Cada auditoría tiene tres fases y suma 120 puntos al completarse. Si llevas otra herramienta, el aviso inferior indica exactamente cuál debes equipar.",
                "",
                "CÓMO LEER LOS MENÚS",
                "Los botones están siempre en la franja inferior. El texto explicativo está en el panel central y puede desplazarse: en PC usa la rueda del ratón o PAGE UP/PAGE DOWN; en VR inclina el stick derecho hacia arriba o abajo. El botón VOLVER permanece visible.",
                "",
                "CONTROLES",
                "PC: WASD/flechas mover · ratón mirar · SHIFT correr · E interactuar · ESC pausa/volver · F1 guía. Menús: clic o ↑/↓ y ENTER; guía: rueda o PAGE UP/DOWN para leer.",
                "VR: cabeza mirar · stick izquierdo mover · stick derecho giro por pasos; en la guía, stick derecho ↑/↓ desplaza el texto · gatillo derecho interactuar/seleccionar · grip izquierdo correr · botón secundario izquierdo cambiar herramienta · botón menú/principal izquierdo pausa.",
                "",
                "FINALIZAR",
                "Cuando DB-03 esté operativo, pulsa ESC y selecciona CERRAR TURNO. También puedes CONTINUAR JUGANDO para terminar auditorías pendientes. El turno termina cuando confirmas CERRAR TURNO; después aparece la puntuación y el resumen de tareas."
            });
            BuildPanel(ScreenMode.PlayerGuide, "GUÍA DEL JUGADOR", body,
                new[] { "VOLVER" });
        }

        private void ShowControls()
        {
            BuildPanel(ScreenMode.Controls, "CONTROLES · PC Y VR",
                "<b>PC</b>\n" +
                "WASD / flechas = mover · Ratón = mirar · SHIFT = correr.\n" +
                "E = interactuar con lo que miras · ESC = pausa.\n" +
                "Menús: clic o FLECHAS ARRIBA/ABAJO para elegir y ENTER para confirmar.\n" +
                "1-5 = escáner, ESD, anemómetro, multímetro y tablet.\n\n" +
                "<b>VR · OpenXR</b>\n" +
                "Cabeza = mirar · Stick izq. = mover · Stick der. = giro por pasos.\n" +
                "Gatillo der. = seleccionar/interactuar · Grip izq. = correr.\n" +
                "Botón secundario izq. = cambiar herramienta · Menú/principal izq. = pausa.\n" +
                "Guía larga: stick derecho arriba/abajo para desplazarte.\n\n" +
                "<b>En la sala</b>\n" +
                "Sigue el nombre, distancia y dirección del HUD. Consola cian = estación activa. Terminal verde = auditoría terminada. Las auditorías indican la herramienta requerida.",
                new[] { "VOLVER AL MENÚ" });
        }

        private void ShowOperationsLog()
        {
            logReturnMode = mode;
            string body;
            if (sessionLog.Count == 0)
            {
                string stored = PlayerPrefs.GetString("DCR_LastOperationsLog", "Todavía no se ha completado ningún turno.");
                string[] savedLines = stored.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                body = "NO HAY UN TURNO ACTIVO.\n\nÚLTIMOS REGISTROS GUARDADOS:\n";
                int firstSaved = Mathf.Max(0, savedLines.Length - 4);
                for (int i = firstSaved; i < savedLines.Length; i++)
                    body += "• " + ShortenLogEntry(savedLines[i], 74) + "\n";
            }
            else
            {
                body = "TURNO ACTUAL: " + FormatTime(elapsedSeconds) + "  ·  " + bonusTasksCompleted + "/5 AUDITORÍAS\n" +
                       "FASES PREVENTIVAS: " + GetCompletedSideTaskSteps() + "/15\n\nÚLTIMOS REGISTROS:\n";
                int firstVisible = Mathf.Max(0, sessionLog.Count - 4);
                for (int i = firstVisible; i < sessionLog.Count; i++)
                    body += "• " + ShortenLogEntry(sessionLog[i], 74) + "\n";
            }
            BuildPanel(ScreenMode.OperationsLog, "BITÁCORA OPERATIVA", body,
                new[] { "VOLVER A LA PANTALLA ANTERIOR" });
        }

        private static string ShortenLogEntry(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
            return value.Substring(0, Mathf.Max(1, maxLength - 3)) + "...";
        }

        private void ShowVersionHistory()
        {
            logReturnMode = mode;
            string body =
                "v1.0 · BASE\nSala 3D y cámara de demostración. No había personaje controlable, movimiento propio ni tareas de mantenimiento.\n\n" +
                "v1.1 · JUEGO INICIAL\nSe añadieron movimiento PC/VR, interacción, menús y 5 incidencias con 15 pasos.\n\n" +
                "v1.2 · HERRAMIENTAS\nCinco herramientas, cinco terminales preventivos, bitácora y ajustes guardados.\n\n" +
                "v1.3 · CONTENIDO\n30 decisiones: 15 pasos principales y 15 fases preventivas. Libre recorrido tras restaurar DB-03.\n\n" +
                "v1.4.5 · TEXTO UGUI\nSe migraron textos a Unity UI Text y Liberation Sans incluida en Resources.\n\n" +
                "v1.4.6 · TIPOGRAFÍA Y HUD COMPACTO\nSe ajustaron tamaños y ubicación del HUD, avisos y etiquetas.\n\n" +
                "v1.5.0 · INTERFAZ FRONTAL Y GUÍA (ACTUAL)\nEn PC todos los menús, botones, HUD, avisos y objetivo usan un Canvas Screen Space Overlay. En VR usan Screen Space Camera con una cámara Overlay URP que se apila después de la cámara de sala, renderiza solo la capa UI y limpia profundidad. Ninguna interfaz se coloca en el espacio de los racks/servidores. El objetivo queda en una banda fija del HUD, no sobre el asset. La guía del jugador explica las cinco incidencias, las cinco herramientas y el cierre del turno; está disponible desde menú/pausa, desde el briefing y con F1.";
            BuildPanel(ScreenMode.VersionHistory, "HISTORIAL DE CAMBIOS", body,
                new[] { "VOLVER A LA PANTALLA ANTERIOR" });
        }

        private void RestoreLogReturnPage()
        {
            if (logReturnMode == ScreenMode.Pause)
                ShowPauseMenu();
            else
                ShowMainMenu();
        }

        private void ShowOptions()
        {
            optionsReturnMode = mode;
            string body = "Selecciona una opción para cambiarla. El valor actual aparece en el propio botón y se guarda automáticamente.\n\n" +
                          "Ratón: sensibilidad de cámara · Audio: volumen general · VR: giro por pasos.\n" +
                          "La velocidad modifica el desplazamiento en PC y VR.";
            BuildPanel(ScreenMode.Options, "CONFIGURACIÓN", body,
                new[]
                {
                    "SENSIBILIDAD RATÓN · " + lookSensitivity.ToString("0.000"),
                    "VOLUMEN GENERAL · " + Mathf.RoundToInt(masterVolume * 100f) + "%",
                    "INVERTIR VERTICAL · " + (invertY ? "ACTIVADO" : "DESACTIVADO"),
                    "GIRO VR · " + snapTurnDegrees.ToString("0") + "°",
                    "VELOCIDAD · " + walkSpeed.ToString("0.0") + " m/s",
                    "VOLVER"
                });
        }

        private void RestoreOptionsReturnPage()
        {
            if (optionsReturnMode == ScreenMode.Pause)
                ShowPauseMenu();
            else
                ShowMainMenu();
        }

        private void ShowPauseMenu()
        {
            string title = mainServiceComplete ? "SERVICIO RESTAURADO" : "TURNO EN PAUSA";
            string description = mainServiceComplete
                ? "DB-03 ya está operativo. Puedes continuar para completar auditorías pendientes o cerrar el turno. El tiempo está detenido."
                : "El turno está detenido. Consulta la guía, ajusta los controles, revisa la bitácora o reinicia el turno.";
            string thirdOption = mainServiceComplete ? "CERRAR TURNO · VER RESULTADOS" : "REINICIAR TURNO · PERDER AVANCE";
            BuildPanel(ScreenMode.Pause, title, description,
                new[] { "CONTINUAR JUGANDO", "GUÍA DEL JUGADOR", "CAMBIAR CONFIGURACIÓN", thirdOption, "VER BITÁCORA", "SALIR AL MENÚ PRINCIPAL" });
        }



        private void BuildPanel(ScreenMode newMode, string title, string body, string[] buttonLabels)
        {
            mode = newMode;
            if (panelRoot != null) Destroy(panelRoot);
            bodyScrollRect = null;
            panelButtons.Clear();
            keyboardMenuSelection = 0;
            hoveredButton = null;
            if (hoveredMaintenanceAction != null) hoveredMaintenanceAction.SetHovered(false);
            hoveredMaintenanceAction = null;
            if (overlayCanvas == null) return;

            panelRoot = new GameObject("DCR_ScreenMenuPanel", typeof(RectTransform), typeof(Image));
            panelRoot.layer = DCRUIStyle.UILayer;
            RectTransform rootRect = panelRoot.GetComponent<RectTransform>();
            rootRect.SetParent(overlayCanvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image dimmer = panelRoot.GetComponent<Image>();
            dimmer.color = new Color(0.002f, 0.008f, 0.014f, 0.82f);
            dimmer.raycastTarget = false;

            // The frame uses normalized safe-area anchors. Every menu element is now
            // screen UI; no panel/button is placed in the 3D room.
            RectTransform frame = CreateUIRect(panelRoot.transform, "Menu_Frame",
                new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.975f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.012f, 0.035f, 0.052f, 0.99f));
            CreateUIRect(frame, "Menu_TopAccent", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, 8f), Vector2.zero, new Color(0.05f, 0.68f, 0.84f, 1f));
            CreateUIRect(frame, "Menu_HeaderRule", new Vector2(0.06f, 0.875f), new Vector2(0.94f, 0.875f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.08f, 0.34f, 0.43f, 1f));

            CreateUIText(frame, "Menu_Title", title, DCRTextRole.PanelTitle,
                new Color(0.80f, 0.96f, 1f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.055f, 0.89f), new Vector2(0.945f, 0.98f), Vector2.zero, Vector2.zero);

            int optionCount = Mathf.Max(1, buttonLabels != null ? buttonLabels.Length : 0);
            float buttonHeight = optionCount >= 7 ? 0.052f :
                                 optionCount >= 6 ? 0.060f :
                                 optionCount >= 5 ? 0.070f :
                                 optionCount >= 4 ? 0.083f :
                                 optionCount >= 3 ? 0.095f :
                                 optionCount == 2 ? 0.105f : 0.12f;
            float buttonGap = optionCount >= 6 ? 0.012f : 0.017f;
            float buttonBottom = 0.045f;
            float buttonArea = optionCount * buttonHeight + Mathf.Max(0, optionCount - 1) * buttonGap;
            float bodyBottom = buttonBottom + buttonArea + 0.025f;
            float bodyTop = 0.835f;
            // For unusually long option lists, preserve a minimum reading area by
            // compressing gaps slightly instead of letting buttons overlap the copy.
            if (bodyBottom > bodyTop - 0.16f)
            {
                buttonGap = Mathf.Max(0.006f, buttonGap - 0.004f);
                buttonArea = optionCount * buttonHeight + Mathf.Max(0, optionCount - 1) * buttonGap;
                bodyBottom = buttonBottom + buttonArea + 0.02f;
            }

            TextAnchor bodyAlignment = newMode == ScreenMode.MainMenu ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
            RectTransform bodyViewport = CreateUIRect(frame, "Menu_Body_Viewport",
                new Vector2(0.075f, bodyBottom), new Vector2(0.925f, bodyTop),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            Image viewportImage = bodyViewport.GetComponent<Image>();
            viewportImage.raycastTarget = false;
            RectMask2D bodyMask = bodyViewport.gameObject.AddComponent<RectMask2D>();
            bodyMask.padding = Vector4.zero;

            bodyScrollRect = bodyViewport.gameObject.AddComponent<ScrollRect>();
            bodyScrollRect.viewport = bodyViewport;
            bodyScrollRect.horizontal = false;
            bodyScrollRect.vertical = true;
            bodyScrollRect.movementType = ScrollRect.MovementType.Clamped;
            bodyScrollRect.inertia = false;
            bodyScrollRect.scrollSensitivity = 30f;
            bodyScrollRect.elasticity = 0.08f;
            bodyScrollRect.velocity = Vector2.zero;

            GameObject bodyContentObject = new GameObject("Menu_Body_Content", typeof(RectTransform));
            bodyContentObject.layer = DCRUIStyle.UILayer;
            RectTransform bodyContent = bodyContentObject.GetComponent<RectTransform>();
            bodyContent.SetParent(bodyViewport, false);
            bodyContent.anchorMin = new Vector2(0f, 1f);
            bodyContent.anchorMax = new Vector2(1f, 1f);
            bodyContent.pivot = new Vector2(0.5f, 1f);
            bodyContent.anchoredPosition = Vector2.zero;
            bodyContent.sizeDelta = new Vector2(0f, Mathf.Max(100f, bodyViewport.rect.height));

            Text bodyText = CreateUIText(bodyContent, "Menu_Body", body, DCRTextRole.PanelBody,
                new Color(0.87f, 0.92f, 0.95f, 1f), bodyAlignment,
                Vector2.zero, Vector2.one, new Vector2(4f, 6f), new Vector2(-4f, -6f));
            if (bodyText != null)
            {
                bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                bodyText.verticalOverflow = VerticalWrapMode.Overflow;
                bodyText.resizeTextForBestFit = false;
                bodyText.fontSize = 24;
                bodyText.lineSpacing = 1.08f;
            }

            Canvas.ForceUpdateCanvases();
            float viewportHeight = Mathf.Max(100f, bodyViewport.rect.height);
            // Use UGUI's own text layout measurement after the viewport width is known.
            // This respects the actual font metrics, rich-text tags and wrapped Spanish copy.
            float measuredTextHeight = bodyText != null ? bodyText.preferredHeight : viewportHeight;
            float contentHeight = Mathf.Max(viewportHeight, measuredTextHeight + 28f);
            bodyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            bodyScrollRect.content = bodyContent;
            bodyScrollRect.verticalNormalizedPosition = 1f;
            bool bodyNeedsScroll = contentHeight > viewportHeight + 2f;

            if (bodyNeedsScroll)
            {
                RectTransform scrollHintPlate = CreateUIRect(frame, "Menu_Scroll_Hint_Plate",
                    new Vector2(0.46f, bodyBottom - 0.024f), new Vector2(0.925f, bodyBottom - 0.002f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                    new Color(0.02f, 0.12f, 0.16f, 0.96f));
                Text scrollHint = CreateUIText(scrollHintPlate, "Menu_Scroll_Hint",
                    playerController != null && playerController.IsXRActive
                        ? "DESPLAZA TEXTO: STICK DERECHO ARRIBA / ABAJO"
                        : "DESPLAZA TEXTO: RUEDA DEL RATÓN · PAGE UP / PAGE DOWN",
                    DCRTextRole.WorldLabel, new Color(0.62f, 0.88f, 0.92f, 1f), TextAnchor.MiddleRight,
                    Vector2.zero, Vector2.one, new Vector2(8f, 1f), new Vector2(-8f, -1f));
                if (scrollHint != null)
                {
                    scrollHint.fontSize = 17;
                    scrollHint.resizeTextForBestFit = true;
                    scrollHint.resizeTextMinSize = 14;
                    scrollHint.resizeTextMaxSize = 17;
                }
            }

            if (buttonLabels != null)
            {
                for (int i = 0; i < buttonLabels.Length; i++)
                {
                    float yMin = buttonBottom + i * (buttonHeight + buttonGap);
                    float yMax = yMin + buttonHeight;
                    int displayIndex = i;
                    string label = (newMode == ScreenMode.Procedure || newMode == ScreenMode.SideTask) && i < 3
                        ? (i + 1) + ".  " + buttonLabels[i]
                        : buttonLabels[i];
                    CreatePanelButton(frame, displayIndex, label, yMin, yMax);
                }
            }

            RefreshKeyboardMenuSelection();
            SetCursorState();
            if (hudRoot != null) hudRoot.SetActive(false);
            if (promptRoot != null) promptRoot.SetActive(false);
            if (objectiveMarkerRoot != null) objectiveMarkerRoot.gameObject.SetActive(false);
            if (crosshairText != null) crosshairText.gameObject.SetActive(false);
            if (xrMenuCursorRoot != null) xrMenuCursorRoot.gameObject.SetActive(false);
        }

        private void CreatePanelButton(Transform parent, int index, string label, float yMin, float yMax)
        {
            RectTransform buttonRect = CreateUIRect(parent, "MenuButton_" + index,
                new Vector2(0.08f, yMin), new Vector2(0.92f, yMax), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.045f, 0.16f, 0.22f, 1f), true);
            Image buttonImage = buttonRect.GetComponent<Image>();
            DCRWorldButton worldButton = buttonRect.gameObject.AddComponent<DCRWorldButton>();
            worldButton.Configure(this, index, buttonImage, label);
            panelButtons.Add(worldButton);

            CreateUIText(buttonRect, "ButtonLabel", label, DCRTextRole.PanelButton,
                Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(14f, 2f), new Vector2(-14f, -2f));
        }

        private void StartMission()
        {
            currentMission = 0;
            currentStep = 0;
            mainServiceComplete = false;
            mistakes = 0;
            score = 1000;
            elapsedSeconds = 0f;
            bonusTasksCompleted = 0;
            selectedToolIndex = 0;
            activeSideTaskIndex = -1;
            for (int i = 0; i < sideTaskProgress.Length; i++) sideTaskProgress[i] = 0;
            sessionLog.Clear();
            sessionLog.Add("TURNO INICIADO: ticket DB-03 abierto y kit preparado.");
            feedback = "Turno iniciado. Dirígete a la estación marcada.";
            feedbackUntil = Time.time + 4f;
            activeProcedureStation = null;
            for (int i = 0; i < maintenanceActions.Length; i++)
            {
                if (maintenanceActions[i] != null)
                {
                    maintenanceActions[i].SetCompleted(false);
                    maintenanceActions[i].SetProgress(0, 3);
                }
            }
            if (playerRig != null)
                playerRig.SetPositionAndRotation(initialRigPosition, initialRigRotation);
            if (playerController != null && !playerController.IsXRActive)
                playerController.ResetDesktopView();
            mode = ScreenMode.Gameplay;
            if (panelRoot != null) Destroy(panelRoot);
            panelRoot = null;
            panelButtons.Clear();
            if (objectiveMarkerRoot != null) objectiveMarkerRoot.gameObject.SetActive(true);
            if (objectiveMarkerRoot != null) objectiveMarkerRoot.gameObject.SetActive(true);
            hoveredButton = null;
            SetStationStates();
            if (hudRoot != null) hudRoot.SetActive(true);
            if (promptRoot != null) promptRoot.SetActive(true);
            if (objectiveMarkerRoot != null) objectiveMarkerRoot.gameObject.SetActive(true);
            if (crosshairText != null) crosshairText.gameObject.SetActive(true);
            if (xrMenuCursorRoot != null) xrMenuCursorRoot.gameObject.SetActive(false);
            SetCursorState();
        }

        private void OpenProcedure(DCRStation station)
        {
            if (station == null || mode != ScreenMode.Gameplay) return;
            activeProcedureStation = station;
            BuildProcedurePanel(null);
        }

        private void UseMaintenanceAction(DCRMaintenanceAction action)
        {
            if (action == null || action.IsCompleted) return;
            int index = action.ActionIndex;
            if (sideTasks == null || index < 0 || index >= sideTasks.Length) return;
            if (!mainServiceComplete)
            {
                feedback = "AUDITORÍAS PREVENTIVAS BLOQUEADAS · PRIMERO RESTAURA DB-03 CON LAS CINCO INCIDENCIAS PRINCIPALES.";
                feedbackUntil = Time.time + 4f;
                PlayFeedback(errorTone);
                return;
            }

            SideTaskDefinition task = sideTasks[index];
            if (selectedToolIndex != index)
            {
                feedback = "HERRAMIENTA INCORRECTA. EQUIPA: " + task.RequiredTool +
                           " (TECLA " + (index + 1) + ")";
                feedbackUntil = Time.time + 3f;
                PlayFeedback(errorTone);
                return;
            }

            activeSideTaskIndex = index;
            BuildSideTaskPanel(null);
        }

        private void BuildSideTaskPanel(string warning)
        {
            if (activeSideTaskIndex < 0 || activeSideTaskIndex >= sideTasks.Length) return;
            SideTaskDefinition task = sideTasks[activeSideTaskIndex];
            int stepIndex = Mathf.Clamp(sideTaskProgress[activeSideTaskIndex], 0, task.Steps.Length - 1);
            ProcedureStep step = task.Steps[stepIndex];
            sideTaskCorrectChoiceIndex = (activeSideTaskIndex + stepIndex + 1) % 3;
            sideTaskChoices[sideTaskCorrectChoiceIndex] = step.Correct;
            int wrong = 0;
            for (int i = 0; i < sideTaskChoices.Length; i++)
            {
                if (i != sideTaskCorrectChoiceIndex)
                    sideTaskChoices[i] = step.Incorrect[wrong++];
            }

            string body = "AUDITORÍA " + (activeSideTaskIndex + 1) + "/" + sideTasks.Length +
                          "  ·  FASE " + (stepIndex + 1) + "/" + task.Steps.Length +
                          "\n<b>HERRAMIENTA</b>  " + task.RequiredTool +
                          "\n<b>OBJETIVO</b>  " + step.Instruction +
                          "\n\n<b>SEGURIDAD</b>  " + step.Rationale +
                          "\n\n<b>ELIGE 1 DE LAS 3 ACCIONES</b>";
            if (!string.IsNullOrEmpty(warning))
                body = "<color=#ffba65>" + warning + "</color>\n\n" + body;

            BuildPanel(ScreenMode.SideTask, task.Title, body,
                new[] { sideTaskChoices[0], sideTaskChoices[1], sideTaskChoices[2], "VOLVER A LA SALA · GUARDAR FASE" });
        }

        private void ChooseSideTaskAction(int choice)
        {
            if (activeSideTaskIndex < 0 || activeSideTaskIndex >= sideTasks.Length) return;
            if (choice == 3)
            {
                activeSideTaskIndex = -1;
                ClosePanelToGameplay();
                return;
            }

            SideTaskDefinition task = sideTasks[activeSideTaskIndex];
            int stepIndex = Mathf.Clamp(sideTaskProgress[activeSideTaskIndex], 0, task.Steps.Length - 1);
            ProcedureStep step = task.Steps[stepIndex];
            if (choice == sideTaskCorrectChoiceIndex)
            {
                PlayFeedback(successTone);
                sessionLog.Add("PREVENTIVO VALIDADO: " + task.Title + " · " + step.Heading);
                sideTaskProgress[activeSideTaskIndex]++;
                DCRMaintenanceAction progressAction = maintenanceActions[activeSideTaskIndex];
                if (progressAction != null)
                    progressAction.SetProgress(sideTaskProgress[activeSideTaskIndex], task.Steps.Length);

                if (sideTaskProgress[activeSideTaskIndex] >= task.Steps.Length)
                {
                    DCRMaintenanceAction action = maintenanceActions[activeSideTaskIndex];
                    if (action != null) action.SetCompleted(true);
                    bonusTasksCompleted++;
                    score = Mathf.Min(1600, score + 120);
                    sessionLog.Add("AUDITORÍA COMPLETA: " + task.Title + " (+120 puntos)");
                    feedback = "AUDITORÍA COMPLETA: " + task.Title + " (+120)";
                    feedbackUntil = Time.time + 3.5f;
                    activeSideTaskIndex = -1;
                    if (mainServiceComplete && bonusTasksCompleted >= sideTasks.Length)
                    {
                        ShowResults();
                        return;
                    }
                    ClosePanelToGameplay();
                }
                else
                {
                    feedback = "FASE VALIDADA: " + step.Heading;
                    feedbackUntil = Time.time + 2.0f;
                    BuildSideTaskPanel(null);
                }
            }
            else
            {
                mistakes++;
                score = Mathf.Max(0, score - 20);
                sessionLog.Add("PREVENTIVO INCORRECTO: " + task.Title + " · " + step.Heading + " (reintento)");
                PlayFeedback(errorTone);
                BuildSideTaskPanel("LECTURA NO VÁLIDA. Revisa la condición y repite esta fase.");
            }
        }

        private int GetCompletedSideTaskSteps()
        {
            if (sideTaskProgress == null) return 0;
            int completed = 0;
            for (int i = 0; i < sideTaskProgress.Length; i++)
                completed += Mathf.Clamp(sideTaskProgress[i], 0, 3);
            return completed;
        }

        private void BuildProcedurePanel(string warning)
        {
            MissionDefinition mission = missions[currentMission];
            ProcedureStep step = mission.Steps[currentStep];
            correctChoiceIndex = (currentMission + currentStep) % 3;
            currentChoices = new string[3];
            currentChoices[correctChoiceIndex] = step.Correct;
            int wrongIndex = 0;
            for (int i = 0; i < 3; i++)
            {
                if (string.IsNullOrEmpty(currentChoices[i]))
                    currentChoices[i] = step.Incorrect[wrongIndex++];
            }

            string body = "INCIDENCIA " + (currentMission + 1) + "/" + missions.Length +
                          "  ·  PASO " + (currentStep + 1) + "/" + mission.Steps.Length +
                          "\n<b>SÍNTOMA</b>  " + mission.Description +
                          "\n\n<b>QUÉ HACER</b>  " + step.Instruction +
                          "\n\n<b>POR QUÉ</b>  " + step.Rationale +
                          "\n\n<b>ELIGE 1 DE LAS 3 ACCIONES</b>";
            if (!string.IsNullOrEmpty(warning))
                body = "<color=#ffba65>" + warning + "</color>\n\n" + body;

            BuildPanel(ScreenMode.Procedure, mission.Title, body,
                new[] { currentChoices[0], currentChoices[1], currentChoices[2], "VOLVER A LA SALA" });
        }

        private void ChooseProcedureAction(int choice)
        {
            if (choice == 3)
            {
                activeProcedureStation = null;
                ClosePanelToGameplay();
                return;
            }

            if (choice == correctChoiceIndex)
            {
                PlayFeedback(successTone);
                feedback = "PASO VALIDADO: " + missions[currentMission].Steps[currentStep].Heading;
                feedbackUntil = Time.time + 3.5f;
                sessionLog.Add("PROCEDIMIENTO VALIDADO: " + missions[currentMission].Steps[currentStep].Heading);
                currentStep++;

                if (currentStep >= missions[currentMission].Steps.Length)
                {
                    feedback = "INCIDENCIA RESUELTA: " + missions[currentMission].Title;
                    feedbackUntil = Time.time + 4f;
                    currentMission++;
                    currentStep = 0;
                    activeProcedureStation = null;
                    if (currentMission >= missions.Length)
                    {
                        mainServiceComplete = true;
                        sessionLog.Add("SERVICIO PRINCIPAL RESTAURADO: DB-03 responde y queda habilitado el cierre de turno.");
                        if (bonusTasksCompleted >= sideTasks.Length)
                        {
                            ShowResults();
                            return;
                        }
                        feedback = "SERVICIO RESTAURADO. Completa auditorías o pulsa ESC, luego elige CERRAR TURNO.";
                        feedbackUntil = Time.time + 6f;
                    }
                    SetStationStates();
                }
                ClosePanelToGameplay();
            }
            else
            {
                PlayFeedback(errorTone);
                mistakes++;
                score = Mathf.Max(0, score - 65);
                sessionLog.Add("ORDEN NO VÁLIDA: " + missions[currentMission].Steps[currentStep].Heading + " (reintento)");
                BuildProcedurePanel("ORDEN INCORRECTO. Revisa el procedimiento y evita intervenir el equipo sin aislarlo.");
            }
        }

        private void SetStationStates()
        {
            if (stations == null) return;
            int activeStationIndex = currentMission == 4 ? 0 : currentMission;
            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i] != null)
                {
                    bool current = mode == ScreenMode.Gameplay && i == activeStationIndex;
                    bool completed = i < currentMission && !(currentMission == 4 && i == 0);
                    stations[i].SetState(current, completed);
                    stations[i].SetMissionIndex(i);
                }
            }
            if (currentMission == 4 && stations[0] != null)
            {
                stations[0].SetMissionIndex(4);
                stations[0].SetState(mode == ScreenMode.Gameplay, false);
            }
        }

        private DCRStation GetCurrentStation()
        {
            if (stations == null || stations.Length == 0) return null;
            return currentMission == 4 ? stations[0] : (currentMission >= 0 && currentMission < stations.Length ? stations[currentMission] : null);
        }

        private void ClosePanelToGameplay()
        {
            mode = ScreenMode.Gameplay;
            if (panelRoot != null) Destroy(panelRoot);
            panelRoot = null;
            panelButtons.Clear();
            hoveredButton = null;
            SetStationStates();
            if (hudRoot != null) hudRoot.SetActive(true);
            if (promptRoot != null) promptRoot.SetActive(true);
            if (objectiveMarkerRoot != null) objectiveMarkerRoot.gameObject.SetActive(true);
            if (crosshairText != null) crosshairText.gameObject.SetActive(true);
            if (xrMenuCursorRoot != null) xrMenuCursorRoot.gameObject.SetActive(false);
            SetCursorState();
        }

        private void ResumeMission()
        {
            ClosePanelToGameplay();
        }

        private void ShowResults()
        {
            int finalScore = Mathf.Max(0, score - Mathf.FloorToInt(elapsedSeconds * 0.75f));
            string grade = finalScore >= 1280 ? "A" : (finalScore >= 960 ? "B" : "C");
            int best = PlayerPrefs.GetInt("DCR_BestScore", 0);
            if (finalScore > best) PlayerPrefs.SetInt("DCR_BestScore", finalScore);
            PlayerPrefs.SetInt("DCR_CompletedShifts", PlayerPrefs.GetInt("DCR_CompletedShifts", 0) + 1);
            sessionLog.Add("TURNO CERRADO: " + finalScore + "/1600, " + bonusTasksCompleted + "/5 auditorías preventivas, " + FormatTime(elapsedSeconds) + ".");
            PlayerPrefs.SetString("DCR_LastOperationsLog", string.Join("\n", sessionLog));
            PlayerPrefs.Save();

            string time = FormatTime(elapsedSeconds);
            BuildPanel(ScreenMode.Results, "TURNO COMPLETADO",
                "SERVICIO DB-03: OPERATIVO\n\nCalificación: " + grade +
                "\nPuntuación: " + finalScore + "/1600" +
                "\nTiempo: " + time +
                "\nErrores de procedimiento: " + mistakes +
                "\nTareas preventivas: " + bonusTasksCompleted + "/" + sideTasks.Length +
                "\nFases preventivas: " + GetCompletedSideTaskSteps() + "/15" +
                "\n\nMejor puntuación y bitácora guardadas localmente. La sala queda lista para el siguiente turno.",
                new[] { "REPETIR MISIÓN", "VOLVER AL MENÚ" });
            SetStationStates();
        }

        private void OnButtonPressed(int index)
        {
            PlayFeedback(clickTone);
            switch (mode)
            {
                case ScreenMode.MainMenu:
                    if (index == 0) ShowTutorial();
                    else if (index == 1) ShowPlayerGuide(ScreenMode.MainMenu);
                    else if (index == 2) ShowOperationsLog();
                    else if (index == 3) ShowVersionHistory();
                    else if (index == 4) ShowOptions();
                    else if (index == 5) ShowControls();
                    else if (index == 6) Application.Quit();
                    break;

                case ScreenMode.Tutorial:
                    if (index == 0) StartMission();
                    else if (index == 1) ShowPlayerGuide(ScreenMode.Tutorial);
                    else ShowMainMenu();
                    break;

                case ScreenMode.PlayerGuide:
                    if (guideReturnMode == ScreenMode.Pause) ShowPauseMenu();
                    else if (guideReturnMode == ScreenMode.Tutorial) ShowTutorial();
                    else if (guideReturnMode == ScreenMode.MainMenu) ShowMainMenu();
                    else ClosePanelToGameplay();
                    break;

                case ScreenMode.Controls:
                    ShowMainMenu();
                    break;

                case ScreenMode.OperationsLog:
                case ScreenMode.VersionHistory:
                    RestoreLogReturnPage();
                    break;

                case ScreenMode.Options:
                    if (index == 0)
                        lookSensitivity = lookSensitivity < 0.07f ? 0.085f : (lookSensitivity < 0.10f ? 0.12f : 0.055f);
                    else if (index == 1)
                        masterVolume = masterVolume < 0.65f ? 0.8f : (masterVolume < 0.9f ? 1f : 0.5f);
                    else if (index == 2) invertY = !invertY;
                    else if (index == 3) snapTurnDegrees = snapTurnDegrees >= 60f ? 30f : snapTurnDegrees + 15f;
                    else if (index == 4) walkSpeed = walkSpeed >= 4.1f ? 2.6f : walkSpeed + 0.5f;
                    else if (index == 5) { SaveSettings(); RestoreOptionsReturnPage(); return; }
                    SaveSettings();
                    ShowOptionsKeepingReturnPage();
                    break;

                case ScreenMode.Procedure:
                    ChooseProcedureAction(index);
                    break;

                case ScreenMode.SideTask:
                    ChooseSideTaskAction(index);
                    break;

                case ScreenMode.Pause:
                    if (index == 0) ResumeMission();
                    else if (index == 1) ShowPlayerGuide(ScreenMode.Pause);
                    else if (index == 2) ShowOptions();
                    else if (index == 3)
                    {
                        if (mainServiceComplete) ShowResults();
                        else StartMission();
                    }
                    else if (index == 4) ShowOperationsLog();
                    else if (index == 5) ShowMainMenu();
                    break;

                case ScreenMode.Results:
                    if (index == 0) StartMission();
                    else ShowMainMenu();
                    break;
            }
        }



        private void ShowOptionsKeepingReturnPage()
        {
            ScreenMode savedReturn = optionsReturnMode;
            ShowOptions();
            optionsReturnMode = savedReturn;
        }

        private void SetCursorState()
        {
            bool xr = playerController != null && playerController.IsXRActive;
            cursorIsLocked = !xr && mode == ScreenMode.Gameplay;
            if (xr)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else if (cursorIsLocked)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void WarpCursorToCenter()
        {
            if (playerController != null && playerController.IsXRActive) return;
            if (Mouse.current != null && mode != ScreenMode.Gameplay)
                Mouse.current.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private bool IsValidActionIndex(int index)
        {
            return sideTasks != null && index >= 0 && index < sideTasks.Length;
        }

        private static string GetSideTaskDisplayName(int index)
        {
            switch (index)
            {
                case 0: return "RED / PUERTOS";
                case 1: return "PROTECCIÓN ESD";
                case 2: return "FLUJO DE AIRE";
                case 3: return "UPS / RESPALDO";
                case 4: return "DOCUMENTACIÓN";
                default: return "AUDITORÍA";
            }
        }

        private DCRMaintenanceAction GetNearestPendingAction()
        {
            DCRMaintenanceAction nearest = null;
            float nearestDistance = float.MaxValue;
            if (maintenanceActions == null || playerRig == null) return null;

            for (int i = 0; i < maintenanceActions.Length; i++)
            {
                DCRMaintenanceAction action = maintenanceActions[i];
                if (action == null || action.IsCompleted) continue;
                float distance = Vector3.Distance(playerRig.position, action.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = action;
                }
            }
            return nearest;
        }

        private void GetCurrentObjective(out string objectiveName, out Vector3 objectivePosition, out string equipmentHint)
        {
            objectiveName = "SERVICIO RESTAURADO";
            objectivePosition = playerRig != null ? playerRig.position : Vector3.zero;
            equipmentHint = string.Empty;

            if (!mainServiceComplete)
            {
                DCRStation station = GetCurrentStation();
                if (station != null)
                {
                    objectiveName = station.StationName;
                    objectivePosition = station.transform.position;
                }
                return;
            }

            DCRMaintenanceAction pending = GetNearestPendingAction();
            if (pending != null && IsValidActionIndex(pending.ActionIndex))
            {
                objectiveName = GetSideTaskDisplayName(pending.ActionIndex);
                objectivePosition = pending.transform.position;
                equipmentHint = " · KIT " + (pending.ActionIndex + 1);
            }
            else if (bonusTasksCompleted >= sideTasks.Length)
            {
                objectiveName = "CERRAR TURNO";
            }
        }

        private string GetObjectiveHint()
        {
            if (playerRig == null || playerCamera == null) return string.Empty;

            GetCurrentObjective(out string objectiveName, out Vector3 objectivePosition, out string equipmentHint);
            if (objectiveName == "CERRAR TURNO")
                return "<b>TODAS LAS TAREAS COMPLETADAS</b>  ·  PULSA ESC PARA ABRIR EL MENÚ Y CERRAR EL TURNO";

            Vector3 toTarget = objectivePosition - playerRig.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            string arrow = GetDirectionLabel(toTarget);
            return "<b>OBJETIVO: " + objectiveName + "</b>  ·  " + distance.ToString("0.0") + " m " + arrow + equipmentHint +
                   "  ·  SIGUE LA DIRECCIÓN Y ACÉRCATE";
        }

        private string GetDirectionLabel(Vector3 toTarget)
        {
            // LiberationSans SDF in this project intentionally ships with Latin
            // glyphs, not the full Unicode arrow block. Use clear Spanish direction
            // words so the objective prompt never depends on missing glyphs.
            if (toTarget.sqrMagnitude < 0.01f) return "EN DESTINO";
            Vector3 forward = playerCamera != null ? playerCamera.transform.forward : playerRig.forward;
            forward.y = 0f;
            forward.Normalize();
            float angle = Vector3.SignedAngle(forward, toTarget.normalized, Vector3.up);
            if (Mathf.Abs(angle) < 22.5f) return "FRENTE";
            if (angle >= 22.5f && angle < 67.5f) return "FRENTE-DERECHA";
            if (angle >= 67.5f && angle < 112.5f) return "DERECHA";
            if (angle >= 112.5f && angle < 157.5f) return "ATRÁS-DERECHA";
            if (angle <= -22.5f && angle > -67.5f) return "FRENTE-IZQUIERDA";
            if (angle <= -67.5f && angle > -112.5f) return "IZQUIERDA";
            if (angle <= -112.5f && angle > -157.5f) return "ATRÁS-IZQUIERDA";
            return "ATRÁS";
        }

        private void UpdateHUD()
        {
            if (mode != ScreenMode.Gameplay || hudText == null) return;
            string missionLine;
            if (mainServiceComplete)
            {
                missionLine = "<b>SERVICIO RESTAURADO</b>  ·  AUDITORÍAS " + bonusTasksCompleted + "/5";
            }
            else
            {
                MissionDefinition mission = missions[Mathf.Clamp(currentMission, 0, missions.Length - 1)];
                missionLine = "INCIDENCIA " + (currentMission + 1) + "/" + missions.Length +
                              "  ·  PASO " + (currentStep + 1) + "/" + mission.Steps.Length;
            }

            GetCurrentObjective(out string objectiveName, out Vector3 objectivePosition, out string equipmentHint);
            Vector3 direction = objectivePosition - playerRig.position;
            direction.y = 0f;
            string objectiveLine = objectiveName == "CERRAR TURNO"
                ? "<color=#79E6A4>OBJETIVO: TURNO LISTO PARA CERRAR</color>"
                : "OBJ: " + objectiveName + " · " + direction.magnitude.ToString("0.0") + " m " + GetDirectionLabel(direction) + equipmentHint;

            string statusLine = FormatTime(elapsedSeconds) + " · " +
                                Mathf.Max(0, score - Mathf.FloorToInt(elapsedSeconds * 0.75f)) + " PTS · " + mistakes + " ERRORES";
            string toolLine = "KIT " + (selectedToolIndex + 1) + "/5 · " + ToolNames[selectedToolIndex] +
                              " | PREV. " + bonusTasksCompleted + "/5 (" + GetCompletedSideTaskSteps() + "/15 FASES)";
            string nextHud = "<b>DATA CENTER RESCUE</b>\n" + objectiveLine + "\n" + missionLine + "\n" +
                             statusLine + "\n" + toolLine;
            if (hudText.text != nextHud)
                hudText.text = nextHud;
            UpdateObjectiveMarker();
        }

        private void UpdateObjectiveMarker()
        {
            if (objectiveMarkerRoot == null || objectiveMarkerText == null || overlayCanvas == null) return;
            if (mode != ScreenMode.Gameplay)
            {
                objectiveMarkerRoot.gameObject.SetActive(false);
                return;
            }

            GetCurrentObjective(out string objectiveName, out Vector3 objectivePosition, out string equipmentHint);
            if (objectiveName == "CERRAR TURNO")
            {
                objectiveMarkerText.text = "SERVICIO RESTAURADO · PULSA ESC PARA CERRAR O CONTINÚA CON LAS AUDITORÍAS";
                objectiveMarkerRoot.gameObject.SetActive(true);
                return;
            }

            float distance = Vector3.Distance(playerRig.position, objectivePosition);
            string direction = GetDirectionLabel(objectivePosition - playerRig.position);
            objectiveMarkerText.text = "SIGUIENTE OBJETIVO: " + objectiveName + "  ·  " + distance.ToString("0.0") + " m  ·  " + direction + equipmentHint;
            objectiveMarkerRoot.gameObject.SetActive(true);
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null)
                    Destroy(runtimeMaterials[i]);
            }
            runtimeMaterials.Clear();

            if (successTone != null) Destroy(successTone);
            if (errorTone != null) Destroy(errorTone);
            if (clickTone != null) Destroy(clickTone);
            DCRUIStyle.ReleaseRuntimeResources();
            RemoveXROverlayCamera();
            if (overlayCanvas != null) Destroy(overlayCanvas.gameObject);
            if (uiEventSystem != null && uiEventSystem.gameObject.name == "DCR_UIEventSystem")
                Destroy(uiEventSystem.gameObject);
        }
    }
}
