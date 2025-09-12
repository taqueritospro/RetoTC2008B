using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Networking;

// Clase que representa una posición en el grid con coordenadas x, y.
[System.Serializable]
public class Posicion
{
    public int x;
    public int y;
}

// Clase que define las propiedades de un bombero en la simulación.
[System.Serializable]
public class Bombero
{
    public int id; // Identificador único del bombero.
    public string tipo; // Tipo de bombero (especialista, generista, etc).
    public Posicion posicion; // Posición actual en el grid.
    public string estado; // Estado actual (activo, inconsciente, etc).
    public int puntosAccion; // Puntos de acción actuales disponibles.
    public int maxPuntosAccion; // Máximo de puntos de acción posibles.
    public bool llevandoVictima; // Indica si está cargando una víctima.
    public Posicion posicionObjetivo; // Posición hacia donde se dirige.
    public string modo; // Modo de operación actual del bombero.
}

// Clase que representa una celda individual del grid de juego.
[System.Serializable]
public class CeldaData
{
    public int x, y; // Coordenadas de la celda.
    public bool esPiso; // Indica si la celda es transitable.
    public bool esDescubierta; // Indica si la celda ha sido explorada.
    public bool tieneFuego; // Indica si hay fuego en esta celda.
    public bool tieneHumo; // Indica si hay humo en esta celda.
    public bool tienePoi; // Indica si hay un punto de interés.
    public string tipoPoi; // Tipo específico del punto de interés.
    public bool tieneVictimaEncontrada; // Indica si hay una víctima encontrada.
    public bool esPuertaCerrada; // Indica si es una puerta cerrada.
    public bool esPuertaAbierta; // Indica si es una puerta abierta.
    public bool esEntrada; // Indica si es una entrada al edificio.
    public int[] paredes; // Array con paredes: [arriba, izquierda, abajo, derecha].
}

// Clase que representa un marcador de posición con tipo específico.
[System.Serializable]
public class MarcadorPosicion
{
    public int x, y; // Coordenadas del marcador.
    public string tipo; // Tipo de marcador.
}

// Clase que agrupa todos los marcadores visuales del juego.
[System.Serializable]
public class Marcadores
{
    public List<MarcadorPosicion> fuego; // Posiciones con fuego activo.
    public List<MarcadorPosicion> humo; // Posiciones con humo activo.
    public List<MarcadorPosicion> pois; // Posiciones con puntos de interés.
    public List<MarcadorPosicion> victimasEncontradas; // Posiciones de víctimas encontradas.
    public List<MarcadorPosicion> puertasCerradas; // Posiciones de puertas cerradas.
    public List<MarcadorPosicion> puertasAbiertas; // Posiciones de puertas abiertas.
    public List<MarcadorPosicion> entradas; // Posiciones de entradas al edificio.
}

// Clase que define las dimensiones del grid de juego.
[System.Serializable]
public class Dimensiones
{
    public int ancho; // Ancho del grid en celdas.
    public int alto; // Alto del grid en celdas.
}

// Clase que contiene todas las estadísticas de la partida actual.
[System.Serializable]
public class Estadisticas
{
    public int turno; // Número del turno actual.
    public int victimasRescatadas; // Víctimas exitosamente rescatadas.
    public int victimasPerdidas; // Víctimas que han muerto.
    public int victimasEncontradas; // Víctimas encontradas pero no rescatadas.
    public int puntosDano; // Puntos de daño acumulados.
    public bool juegoTerminado; // Indica si el juego ha terminado.
    public string resultado; // Resultado final (victoria, derrota, etc).
    public int totalFuegosActivos; // Cantidad total de fuegos activos.
    public int totalHumosActivos; // Cantidad total de humos activos.
    public int totalPoisActivos; // Cantidad total de POIs restantes.
    public int maxPuntosDano; // Máximo de puntos de daño permitidos.
    public int maxVictimasPerdidas; // Máximo de víctimas que pueden morir.
    public int victimasParaGanar; // Víctimas necesarias para ganar.
}

// Clase que representa el estado completo de la simulación en un momento dado.
[System.Serializable]
public class EstadoSimulacion
{
    public int paso; // Número de paso actual.
    public Dimensiones dimensiones; // Dimensiones del grid.
    public List<Bombero> bomberos; // Lista de todos los bomberos.
    public List<CeldaData> grid; // Grid completo del juego.
    public Marcadores marcadores; // Todos los marcadores visuales.
    public Estadisticas estadisticas; // Estadísticas actuales.
    public int timestamp; // Marca temporal del estado.
}

// Clase que representa la respuesta del servidor a nuestras peticiones.
[System.Serializable]
public class RespuestaServidor
{
    public string status; // Estado de la respuesta (success, error).
    public string message; // Mensaje descriptivo de la respuesta.
    public EstadoSimulacion estado; // Estado actual de la simulación.
    public bool auto_run; // Indica si el auto-run está activo.
}

// Clase principal que maneja la comunicación con el servidor y la interfaz de usuario.
public class SimulationClient : MonoBehaviour
{
    // Referencias a las cámaras del juego.
    [Header("Cámaras")]
    public Camera ndCamera; // Cámara en 2D vista superior.
    public Camera rdCamera; // Cámara en 3D renderizada.
    public Camera mainCamera; // Cámara principal por defecto.
    private Camera activeCamera; // Cámara actualmente activa.

    private Camera[] cameras; // Array de todas las cámaras disponibles.
    private int cameraIndex = 0; // Índice de la cámara actual.

    // Variables para el historial de estados y navegación temporal.
    private List<EstadoSimulacion> historialEstados = new List<EstadoSimulacion>();
    private int indiceEstadoActual = -1; // Índice del estado actual en el historial.
    public float stepDelay = 1f; // Retraso entre steps en modo auto-run.
    private Coroutine autoRunCoroutine; // Referencia a la corrutina de auto-run.

    // Configuración de conexión con el servidor.
    [Header("Configuración del Servidor")]
    public string serverUrl = "http://localhost:5000"; // URL base del servidor.

    // Variables de control del estado de la simulación.
    [Header("Estado de la Simulación")]
    public bool simulationActive = false; // Indica si la simulación está activa.
    public bool autoRunActive = false; // Indica si el auto-run está activo.

    // Variables internas para el manejo de datos.
    private EstadoSimulacion estadoActual; // Estado actual de la simulación.
    private Vector2 scrollPosition = Vector2.zero; // Posición del scroll en la UI.

    // Variables para personalizar la interfaz de usuario.
    private GUIStyle titleStyle; // Estilo para títulos.
    private GUIStyle labelStyle; // Estilo para etiquetas normales.
    private GUIStyle jsonStyle; // Estilo para mostrar JSON.
    private bool showDetailedInfo = true; // Toggle para mostrar información detallada.
    private bool showRawJSON = false; // Toggle para mostrar JSON sin procesar.
    private string lastRawJSON = ""; // Último JSON recibido del servidor.

    // Referencia al renderizador del grid.
    private GridRenderer gridRenderer;

    void Start()
    {
        // Mostrar controles disponibles en la consola.
        Debug.Log("SimulationClient iniciado. Controles:");
        Debug.Log("S - Inicializar simulación");
        Debug.Log("SPACE - Ejecutar step");
        Debug.Log("R - Reiniciar simulación");
        Debug.Log("O - Toggle auto-run");
        Debug.Log("P - Pausar auto-run");
        Debug.Log("C - Cambiar cámara");
        Debug.Log("I - Toggle info detallada");
        Debug.Log("J - Toggle JSON raw");

        // Obtener referencia al renderizador del grid.
        gridRenderer = FindFirstObjectByType<GridRenderer>();

        // Inicializar estilos para la interfaz gráfica.
        titleStyle = new GUIStyle();
        titleStyle.fontSize = 24;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle();
        labelStyle.fontSize = 20;
        labelStyle.normal.textColor = Color.white;

        jsonStyle = new GUIStyle();
        jsonStyle.fontSize = 28;
        jsonStyle.normal.textColor = Color.green;
        jsonStyle.wordWrap = true;

        // Configurar cámara principal por defecto.
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Inicializar array de cámaras y activar la primera.
        cameras = new Camera[] { ndCamera, rdCamera, mainCamera };

        if (ndCamera != null)
        {
            cameraIndex = 0;
            ActivarCamara(cameras[cameraIndex]);
        }
    }

    void Update()
    {
        // Detectar input del teclado para controlar la simulación.
        if (Input.GetKeyDown(KeyCode.S))
            StartCoroutine(InicializarSimulacion());
        else if (Input.GetKeyDown(KeyCode.Space))
            StartCoroutine(EjecutarStep());
        else if (Input.GetKeyDown(KeyCode.R))
            StartCoroutine(ReiniciarSimulacion());
        else if (Input.GetKeyDown(KeyCode.O))
            StartAutoRun();
        else if (Input.GetKeyDown(KeyCode.P))
            StopAutoRun();
        else if (Input.GetKeyDown(KeyCode.C))
            CambiarCamara();
        else if (Input.GetKeyDown(KeyCode.I))
            showDetailedInfo = !showDetailedInfo;
        else if (Input.GetKeyDown(KeyCode.J))
            showRawJSON = !showRawJSON;
    }

    // Activa una cámara específica y desactiva las demás.
    private void ActivarCamara(Camera cam)
    {
        // Desactivar todas las cámaras primero.
        foreach (var c in cameras)
        {
            if (c != null) c.enabled = false;
        }

        // Activar la cámara seleccionada.
        activeCamera = cam;
        if (activeCamera != null)
            activeCamera.enabled = true;
    }

    // Cambia a la siguiente cámara en el array circular.
    private void CambiarCamara()
    {
        cameraIndex = (cameraIndex + 1) % cameras.Length;
        ActivarCamara(cameras[cameraIndex]);
        Debug.Log($"Cámara activa: {activeCamera.name}");
    }

    // Corrutina que envía petición al servidor para inicializar la simulación.
    IEnumerator InicializarSimulacion()
    {
        Debug.Log("Inicializando simulación...");

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/inicializar", ""))
        {
            yield return www.SendWebRequest();

            // Verificar si hubo errores en la petición.
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error inicializando: {www.error}");
                yield break;
            }

            try
            {
                // Guardar JSON sin procesar y deserializar la respuesta.
                lastRawJSON = www.downloadHandler.text;
                RespuestaServidor respuesta = JsonConvert.DeserializeObject<RespuestaServidor>(www.downloadHandler.text);
                Debug.Log($"Respuesta: {respuesta.message}");

                // Si la respuesta es exitosa, activar la simulación.
                if (respuesta.status == "success" && respuesta.estado != null)
                {
                    simulationActive = true;
                    ActualizarEstado(respuesta.estado);
                }
                else
                {
                    Debug.LogError("Error: Estado nulo en la respuesta");
                }
            }
            catch (Exception e)
            {
                // Manejo de errores en el parsing del JSON.
                Debug.LogError($"Error parseando JSON: {e.Message}");
                Debug.LogError($"JSON recibido: {www.downloadHandler.text}");
            }
        }
    }

    // Corrutina que ejecuta un paso de la simulación en el servidor.
    IEnumerator EjecutarStep()
    {
        // Verificar que la simulación esté activa antes de continuar.
        if (!simulationActive)
        {
            Debug.LogWarning("Simulación no activa. Presiona S para inicializar.");
            yield break;
        }

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/step", ""))
        {
            yield return www.SendWebRequest();

            // Verificar errores en la petición.
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error ejecutando step: {www.error}");
                yield break;
            }

            try
            {
                // Procesar respuesta del servidor.
                lastRawJSON = www.downloadHandler.text;
                RespuestaServidor respuesta = JsonConvert.DeserializeObject<RespuestaServidor>(www.downloadHandler.text);

                if (respuesta.estado != null)
                {
                    Debug.Log($"Step ejecutado - Turno: {respuesta.estado.estadisticas.turno}");
                    ActualizarEstado(respuesta.estado);

                    // Verificar si el juego ha terminado.
                    if (respuesta.estado.estadisticas.juegoTerminado)
                    {
                        Debug.Log($"¡Juego terminado! Resultado: {respuesta.estado.estadisticas.resultado}");
                        simulationActive = false;
                        autoRunActive = false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parseando step: {e.Message}");
            }
        }
    }

    // Corrutina que reinicia completamente la simulación en el servidor.
    IEnumerator ReiniciarSimulacion()
    {
        Debug.Log("Reiniciando simulación...");

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/reiniciar", ""))
        {
            yield return www.SendWebRequest();

            // Verificar errores en la petición.
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error reiniciando: {www.error}");
                yield break;
            }

            try
            {
                // Procesar respuesta y reactivar la simulación.
                lastRawJSON = www.downloadHandler.text;
                RespuestaServidor respuesta = JsonConvert.DeserializeObject<RespuestaServidor>(www.downloadHandler.text);
                Debug.Log($"Simulación reiniciada: {respuesta.message}");

                if (respuesta.estado != null)
                {
                    simulationActive = true;
                    autoRunActive = false; // Desactivar auto-run al reiniciar.
                    ActualizarEstado(respuesta.estado);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parseando reinicio: {e.Message}");
            }
        }
    }

    // Corrutina que activa o desactiva el modo auto-run en el servidor.
    IEnumerator ToggleAutoRun()
    {
        // Crear JSON para toggle del auto-run.
        string jsonData = $"{{\"activate\": {(!autoRunActive).ToString().ToLower()}}}";

        using (UnityWebRequest www = UnityWebRequest.Put($"{serverUrl}/auto_run", jsonData))
        {
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            // Verificar errores en la petición.
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error toggle auto-run: {www.error}");
                yield break;
            }

            try
            {
                // Actualizar estado del auto-run según respuesta del servidor.
                RespuestaServidor respuesta = JsonConvert.DeserializeObject<RespuestaServidor>(www.downloadHandler.text);
                autoRunActive = respuesta.auto_run;
                Debug.Log($"Auto-run: {(autoRunActive ? "ACTIVADO" : "DESACTIVADO")}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parseando auto-run: {e.Message}");
            }
        }
    }

    // Corrutina que pausa el auto-run en el servidor.
    IEnumerator PausarAutoRun()
    {
        if (autoRunActive)
        {
            string jsonData = "{\"activate\": false}";

            using (UnityWebRequest www = UnityWebRequest.Put($"{serverUrl}/auto_run", jsonData))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                // Si la petición fue exitosa, desactivar auto-run localmente.
                if (www.result == UnityWebRequest.Result.Success)
                {
                    autoRunActive = false;
                    Debug.Log("Auto-run PAUSADO");
                }
            }
        }
    }

    // Actualiza el estado actual y renderiza todos los elementos visuales.
    void ActualizarEstado(EstadoSimulacion estado)
    {
        // Guardar copia profunda del estado en el historial usando JSON.
        historialEstados.Add(JsonConvert.DeserializeObject<EstadoSimulacion>(JsonConvert.SerializeObject(estado)));
        indiceEstadoActual = historialEstados.Count - 1;

        // Verificar que el estado no sea nulo.
        if (estado == null)
        {
            Debug.LogError("Estado recibido es nulo");
            return;
        }

        estadoActual = estado;

        // Mostrar información básica en consola.
        Debug.Log($"=== ESTADO ACTUALIZADO ===");
        Debug.Log($"Turno: {estado.estadisticas?.turno ?? 0}");
        Debug.Log($"Dimensiones: {estado.dimensiones?.ancho ?? 0}x{estado.dimensiones?.alto ?? 0}");

        // Mostrar estadísticas detalladas en consola.
        if (estado.estadisticas != null)
        {
            MostrarEstadisticasConsola(estado.estadisticas);
        }

        // Mostrar información de bomberos en consola.
        if (estado.bomberos != null)
        {
            Debug.Log($"Bomberos en juego: {estado.bomberos.Count}");
            foreach (var bombero in estado.bomberos)
            {
                Debug.Log($"Bombero {bombero.id} ({bombero.tipo}): Pos({bombero.posicion.x},{bombero.posicion.y}) - {bombero.estado} - AP:{bombero.puntosAccion}/{bombero.maxPuntosAccion}");
            }
        }

        // Renderizar el grid base si el renderer está disponible.
        if (gridRenderer != null && estado.grid != null)
        {
            gridRenderer.RenderGrid(estado.grid, estado.dimensiones.ancho, estado.dimensiones.alto);
        }

        // Renderizar fuego en el grid.
        if (gridRenderer != null && estado.marcadores != null)
        {
            gridRenderer.RenderFire(estado.marcadores.fuego);
        }

        // Renderizar humo en el grid.
        if (gridRenderer != null && estado.marcadores != null)
        {
            gridRenderer.RenderSmoke(estado.marcadores.humo);
        }

        // Renderizar posiciones de bomberos.
        if (gridRenderer != null && estado.bomberos != null)
        {
            gridRenderer.RenderFirefighters(estado.bomberos);
        }

        // Renderizar POIs y víctimas encontradas.
        if (gridRenderer != null && estado.marcadores != null)
        {
            gridRenderer.RenderPOIs(estado.marcadores.pois);
            gridRenderer.RenderVictimFound(estado.marcadores.victimasEncontradas);
        }

        // Limpiar listas de puertas antes de actualizarlas.
        gridRenderer.puertasCerradas.Clear();
        gridRenderer.puertasAbiertas.Clear();

        // Procesar puertas cerradas y extraer dirección del tipo.
        foreach (var p in estado.marcadores.puertasCerradas)
        {
            int dir = 0;
            if (!string.IsNullOrEmpty(p.tipo))
                int.TryParse(p.tipo, out dir);
            gridRenderer.puertasCerradas.Add((p.x, p.y, dir));
        }

        // Procesar puertas abiertas y extraer dirección del tipo.
        foreach (var p in estado.marcadores.puertasAbiertas)
        {
            int dir = 0;
            if (!string.IsNullOrEmpty(p.tipo))
                int.TryParse(p.tipo, out dir);
            gridRenderer.puertasAbiertas.Add((p.x, p.y, dir));
        }

        // Renderizar paredes y puertas del grid.
        if (gridRenderer != null && estado.grid != null)
        {
            gridRenderer.RenderWalls(estado.grid, estado.dimensiones.alto);
            gridRenderer.RenderDoors(estado.grid, estado.dimensiones.alto);
        }
    }

    // Muestra estadísticas detalladas en la consola de Unity.
    void MostrarEstadisticasConsola(Estadisticas stats)
    {
        Debug.Log($"=== TURNO {stats.turno} ===");
        Debug.Log($"Víctimas rescatadas: {stats.victimasRescatadas}/{stats.victimasParaGanar}");
        Debug.Log($"Víctimas perdidas: {stats.victimasPerdidas}/{stats.maxVictimasPerdidas}");
        Debug.Log($"Víctimas encontradas: {stats.victimasEncontradas}");
        Debug.Log($"Puntos de daño: {stats.puntosDano}/{stats.maxPuntosDano}");
        Debug.Log($"Fuegos activos: {stats.totalFuegosActivos}");
        Debug.Log($"Humos activos: {stats.totalHumosActivos}");
        Debug.Log($"POIs activos: {stats.totalPoisActivos}");

        // Mostrar resultado final si el juego ha terminado.
        if (stats.juegoTerminado)
        {
            Debug.Log($"¡JUEGO TERMINADO! Resultado: {stats.resultado}");
        }
    }

    // Corrutina que ejecuta steps automáticamente con delay configurado.
    IEnumerator AutoRunSteps()
    {
        while (simulationActive && autoRunActive)
        {
            yield return EjecutarStep(); // Ejecutar un step.
            yield return new WaitForSeconds(stepDelay); // Esperar el delay configurado.
        }
    }

    // Inicia el modo auto-run localmente.
    void StartAutoRun()
    {
        if (!autoRunActive)
        {
            autoRunActive = true;
            autoRunCoroutine = StartCoroutine(AutoRunSteps());
            Debug.Log("Auto-run iniciado");
        }
    }

    // Detiene el modo auto-run localmente.
    void StopAutoRun()
    {
        if (autoRunActive)
        {
            autoRunActive = false;
            if (autoRunCoroutine != null)
                StopCoroutine(autoRunCoroutine);
            Debug.Log("Auto-run detenido");
        }
    }

    // Dibuja la interfaz gráfica de usuario en pantalla.
    void OnGUI()
    {
        // Panel principal de controles.
        GUI.Box(new Rect(10, 10, 280, 340), "");

        GUILayout.BeginArea(new Rect(20, 40, 260, 300));

        // Mostrar lista de controles disponibles.
        GUILayout.Label("CONTROLES", titleStyle);
        GUILayout.Label("S - Inicializar simulación", labelStyle);
        GUILayout.Label("SPACE - Ejecutar step", labelStyle);
        GUILayout.Label("R - Reiniciar simulación", labelStyle);
        GUILayout.Label("O - Toggle auto-run", labelStyle);
        GUILayout.Label("P - Pausar auto-run", labelStyle);
        GUILayout.Label("C - Cambiar cámara", labelStyle);
        GUILayout.Label("I - Toggle info detallada", labelStyle);
        GUILayout.Label("J - Toggle JSON raw", labelStyle);

        GUILayout.Space(10);

        // Mostrar estado actual de la simulación si está disponible.
        if (estadoActual != null && estadoActual.estadisticas != null)
        {
            GUILayout.Label($"Estado: {(simulationActive ? "ACTIVO" : "INACTIVO")}", labelStyle);
            GUILayout.Label($"Auto-run: {(autoRunActive ? "ON" : "OFF")}", labelStyle);
            GUILayout.Label($"Turno: {estadoActual.estadisticas.turno}", labelStyle);
        }

        GUILayout.EndArea();

        // Mostrar panel de estadísticas detalladas si está habilitado.
        if (showDetailedInfo && estadoActual != null)
        {
            MostrarPanelEstadisticas();
        }

        // Mostrar panel de JSON sin procesar si está habilitado.
        if (showRawJSON && !string.IsNullOrEmpty(lastRawJSON))
        {
            MostrarPanelJSON();
        }
    }

    // Muestra un panel detallado con toda la información del juego.
    void MostrarPanelEstadisticas()
    {
        var stats = estadoActual.estadisticas;
        if (stats == null) return;

        // Crear caja contenedora para las estadísticas.
        GUI.Box(new Rect(310, 10, 390, 500), "");

        GUILayout.BeginArea(new Rect(320, 40, 370, 450));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(370), GUILayout.Height(450));

        // Información general del juego.
        GUILayout.Label("ESTADO DEL JUEGO", titleStyle);
        GUILayout.Label($"Turno: {stats.turno}", labelStyle);
        GUILayout.Label($"Paso: {estadoActual.paso}", labelStyle);
        GUILayout.Label($"Timestamp: {estadoActual.timestamp}", labelStyle);
        GUILayout.Label($"Dimensiones: {estadoActual.dimensiones?.ancho ?? 0}x{estadoActual.dimensiones?.alto ?? 0}", labelStyle);
        GUILayout.Label($"Juego terminado: {(stats.juegoTerminado ? "SÍ" : "NO")}", labelStyle);

        // Mostrar resultado si el juego ha terminado.
        if (stats.juegoTerminado && !string.IsNullOrEmpty(stats.resultado))
        {
            GUIStyle resultStyle = new GUIStyle(labelStyle);
            resultStyle.normal.textColor = Color.yellow;
            GUILayout.Label($"Resultado: {stats.resultado}", resultStyle);
        }

        // Información sobre víctimas.
        GUILayout.Space(10);
        GUILayout.Label("VÍCTIMAS", titleStyle);
        GUILayout.Label($"Rescatadas: {stats.victimasRescatadas} / {stats.victimasParaGanar}", labelStyle);
        GUILayout.Label($"Perdidas: {stats.victimasPerdidas} / {stats.maxVictimasPerdidas}", labelStyle);
        GUILayout.Label($"Encontradas: {stats.victimasEncontradas}", labelStyle);

        // Información sobre peligros activos.
        GUILayout.Space(10);
        GUILayout.Label("PELIGROS", titleStyle);
        GUILayout.Label($"Puntos daño: {stats.puntosDano} / {stats.maxPuntosDano}", labelStyle);
        GUILayout.Label($"Fuegos activos: {stats.totalFuegosActivos}", labelStyle);
        GUILayout.Label($"Humos activos: {stats.totalHumosActivos}", labelStyle);
        GUILayout.Label($"POIs restantes: {stats.totalPoisActivos}", labelStyle);

        // Información detallada de cada bombero.
        if (estadoActual.bomberos != null && estadoActual.bomberos.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("BOMBEROS", titleStyle);
            foreach (var bombero in estadoActual.bomberos)
            {
                GUILayout.Label($"ID {bombero.id} - {bombero.tipo}", labelStyle);
                GUILayout.Label($"  Pos: ({bombero.posicion?.x ?? 0},{bombero.posicion?.y ?? 0})", labelStyle);
                GUILayout.Label($"  Estado: {bombero.estado}", labelStyle);
                GUILayout.Label($"  AP: {bombero.puntosAccion}/{bombero.maxPuntosAccion}", labelStyle);
                GUILayout.Label($"  Llevando víctima: {(bombero.llevandoVictima ? "SÍ" : "NO")}", labelStyle);
                GUILayout.Label($"  Modo: {bombero.modo}", labelStyle);
                GUILayout.Space(5);
            }
        }

        // Información sobre marcadores activos en el mapa.
        if (estadoActual.marcadores != null)
        {
            var marc = estadoActual.marcadores;
            GUILayout.Space(10);
            GUILayout.Label("=== MARCADORES ===", titleStyle);
            GUILayout.Label($"Fuegos: {marc.fuego?.Count ?? 0}", labelStyle);
            GUILayout.Label($"Humos: {marc.humo?.Count ?? 0}", labelStyle);
            GUILayout.Label($"POIs: {marc.pois?.Count ?? 0}", labelStyle);
            GUILayout.Label($"Víctimas encontradas: {marc.victimasEncontradas?.Count ?? 0}", labelStyle);
            GUILayout.Label($"Puertas cerradas: {marc.puertasCerradas?.Count ?? 0}", labelStyle);
            GUILayout.Label($"Puertas abiertas: {marc.puertasAbiertas?.Count ?? 0}", labelStyle);
            GUILayout.Label($"Entradas: {marc.entradas?.Count ?? 0}", labelStyle);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // Muestra el JSON sin procesar recibido del servidor en un panel scrolleable.
    void MostrarPanelJSON()
    {
        GUI.Box(new Rect(10, 250, Screen.width - 20, Screen.height - 260), "JSON RAW");

        GUILayout.BeginArea(new Rect(20, 280, Screen.width - 40, Screen.height - 290));

        // Crear área scrolleable para el JSON formateado.
        Vector2 jsonScrollPosition = GUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(Screen.width - 40), GUILayout.Height(Screen.height - 290));
        GUILayout.Label(FormatJSON(lastRawJSON), jsonStyle);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    // Formatea el JSON para mejor legibilidad añadiendo saltos de línea.
    string FormatJSON(string json)
    {
        // Verificar que el JSON no esté vacío.
        if (string.IsNullOrEmpty(json)) return "";

        try
        {
            // Formateo básico añadiendo saltos de línea después de elementos clave.
            return json.Replace(",", ",\n")
                      .Replace("{", "{\n")
                      .Replace("}", "\n}")
                      .Replace("[", "[\n")
                      .Replace("]", "\n]");
        }
        catch
        {
            // Si hay error en el formateo, devolver el JSON original.
            return json;
        }
    }
}