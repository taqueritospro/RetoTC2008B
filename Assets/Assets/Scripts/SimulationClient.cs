using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[System.Serializable]
public class Posicion
{
    public int x;
    public int y;
}

[System.Serializable]
public class Bombero
{
    public int id;
    public string tipo;
    public Posicion posicion;
    public string estado;
    public int puntosAccion;
    public int maxPuntosAccion;
    public bool llevandoVictima;
    public Posicion posicionObjetivo;
    public string modo;
}

[System.Serializable]
public class CeldaData
{
    public int x;
    public int y;
    public bool esPiso;
    public bool esDescubierta;
    public bool tieneFuego;
    public bool tieneHumo;
    public bool tienePoi;
    public string tipoPoi;
    public bool tieneVictimaEncontrada;
    public bool esPuertaCerrada;
    public bool esPuertaAbierta;
    public bool esEntrada;
    public int[] paredes; // [arriba, izquierda, abajo, derecha]
}

[System.Serializable]
public class MarcadorPosicion
{
    public int x;
    public int y;
    public string tipo;
}

[System.Serializable]
public class Marcadores
{
    public List<MarcadorPosicion> fuego;
    public List<MarcadorPosicion> humo;
    public List<MarcadorPosicion> pois;
    public List<MarcadorPosicion> victimasEncontradas;
    public List<MarcadorPosicion> puertasCerradas;
    public List<MarcadorPosicion> puertasAbiertas;
    public List<MarcadorPosicion> entradas;
}

[System.Serializable]
public class Dimensiones
{
    public int ancho;
    public int alto;
}

[System.Serializable]
public class Estadisticas
{
    public int turno;
    public int victimasRescatadas;
    public int victimasPerdidas;
    public int victimasEncontradas;
    public int puntosDano;
    public bool juegoTerminado;
    public string resultado;
    public int totalFuegosActivos;
    public int totalHumosActivos;
    public int totalPoisActivos;
    public int maxPuntosDano;
    public int maxVictimasPerdidas;
    public int victimasParaGanar;
}

[System.Serializable]
public class EstadoSimulacion
{
    public int paso;
    public Dimensiones dimensiones;
    public List<Bombero> bomberos;
    public List<CeldaData> grid;
    public Marcadores marcadores;
    public Estadisticas estadisticas;
    public int timestamp;
}

[System.Serializable]
public class RespuestaServidor
{
    public string status;
    public string message;
    public EstadoSimulacion estado;
    public bool auto_run;
}

public class SimulationClient : MonoBehaviour
{
    [Header("Configuración del Servidor")]
    public string serverUrl = "http://localhost:5000";

    [Header("Estado de la Simulación")]
    public bool simulationActive = false;
    public bool autoRunActive = false;

    // Variables internas
    private EstadoSimulacion estadoActual;
    private Vector2 scrollPosition = Vector2.zero;

    // Variables para UI
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle jsonStyle;
    private bool showDetailedInfo = true;
    private bool showRawJSON = false;
    private string lastRawJSON = "";

    void Start()
    {
        Debug.Log("SimulationClient iniciado. Controles:");
        Debug.Log("S - Inicializar simulación");
        Debug.Log("SPACE - Ejecutar step");
        Debug.Log("R - Reiniciar simulación");
        Debug.Log("O - Toggle auto-run");
        Debug.Log("P - Pausar auto-run");
        Debug.Log("I - Toggle info detallada");
        Debug.Log("J - Toggle JSON raw");
        
        gridRenderer = FindFirstObjectByType<GridRenderer>();

        // Configurar estilos GUI
        titleStyle = new GUIStyle();
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle();
        labelStyle.fontSize = 12;
        labelStyle.normal.textColor = Color.white;

        jsonStyle = new GUIStyle();
        jsonStyle.fontSize = 10;
        jsonStyle.normal.textColor = Color.green;
        jsonStyle.wordWrap = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartCoroutine(InicializarSimulacion());
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(EjecutarStep());
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(ReiniciarSimulacion());
        }
        else if (Input.GetKeyDown(KeyCode.O))
        {
            StartCoroutine(ToggleAutoRun());
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            StartCoroutine(PausarAutoRun());
        }
        else if (Input.GetKeyDown(KeyCode.I))
        {
            showDetailedInfo = !showDetailedInfo;
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            showRawJSON = !showRawJSON;
        }
    }

    IEnumerator InicializarSimulacion()
    {
        Debug.Log("Inicializando simulación...");

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/inicializar", ""))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error inicializando: {www.error}");
                yield break;
            }

            try
            {
                lastRawJSON = www.downloadHandler.text;
                RespuestaServidor respuesta = JsonConvert.DeserializeObject<RespuestaServidor>(www.downloadHandler.text);
                Debug.Log($"Respuesta: {respuesta.message}");

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
                Debug.LogError($"Error parseando JSON: {e.Message}");
                Debug.LogError($"JSON recibido: {www.downloadHandler.text}");
            }
        }
    }

    IEnumerator EjecutarStep()
    {
        if (!simulationActive)
        {
            Debug.LogWarning("Simulación no activa. Presiona S para inicializar.");
            yield break;
        }

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/step", ""))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error ejecutando step: {www.error}");
                yield break;
            }

            try
            {
                lastRawJSON = www.downloadHandler.text;
                RespuestaServidor respuesta = JsonConvert.DeserializeObject<RespuestaServidor>(www.downloadHandler.text);

                if (respuesta.estado != null)
                {
                    Debug.Log($"Step ejecutado - Turno: {respuesta.estado.estadisticas.turno}");
                    ActualizarEstado(respuesta.estado);

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

    IEnumerator ReiniciarSimulacion()
    {
        Debug.Log("Reiniciando simulación...");

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/reiniciar", ""))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error reiniciando: {www.error}");
                yield break;
            }

            try
            {
                lastRawJSON = www.downloadHandler.text;
                RespuestaServidor respuesta = JsonConvert.DeserializeObject<RespuestaServidor>(www.downloadHandler.text);
                Debug.Log($"Simulación reiniciada: {respuesta.message}");

                if (respuesta.estado != null)
                {
                    simulationActive = true;
                    autoRunActive = false;
                    ActualizarEstado(respuesta.estado);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parseando reinicio: {e.Message}");
            }
        }
    }

    IEnumerator ToggleAutoRun()
    {
        string jsonData = $"{{\"activate\": {(!autoRunActive).ToString().ToLower()}}}";

        using (UnityWebRequest www = UnityWebRequest.Put($"{serverUrl}/auto_run", jsonData))
        {
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error toggle auto-run: {www.error}");
                yield break;
            }

            try
            {
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

    IEnumerator PausarAutoRun()
    {
        if (autoRunActive)
        {
            string jsonData = "{\"activate\": false}";

            using (UnityWebRequest www = UnityWebRequest.Put($"{serverUrl}/auto_run", jsonData))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    autoRunActive = false;
                    Debug.Log("Auto-run PAUSADO");
                }
            }
        }
    }

    private GridRenderer gridRenderer;

    void ActualizarEstado(EstadoSimulacion estado)
    {
        if (estado == null)
        {
            Debug.LogError("Estado recibido es nulo");
            return;
        }

        estadoActual = estado;

        Debug.Log($"=== ESTADO ACTUALIZADO ===");
        Debug.Log($"Turno: {estado.estadisticas?.turno ?? 0}");
        Debug.Log($"Dimensiones: {estado.dimensiones?.ancho ?? 0}x{estado.dimensiones?.alto ?? 0}");

        // Mostrar estadísticas en consola
        if (estado.estadisticas != null)
        {
            MostrarEstadisticasConsola(estado.estadisticas);
        }

        // Mostrar información de bomberos
        if (estado.bomberos != null)
        {
            Debug.Log($"Bomberos en juego: {estado.bomberos.Count}");
            foreach (var bombero in estado.bomberos)
            {
                Debug.Log($"Bombero {bombero.id} ({bombero.tipo}): Pos({bombero.posicion.x},{bombero.posicion.y}) - {bombero.estado} - AP:{bombero.puntosAccion}/{bombero.maxPuntosAccion}");
            }
        }
        if (gridRenderer != null && estado.grid != null)
        {
            gridRenderer.RenderGrid(estado.grid, estado.dimensiones.ancho, estado.dimensiones.alto);
        }
        if (gridRenderer != null && estado.marcadores != null)
        {
            gridRenderer.RenderFire(estado.marcadores.fuego);
        }
        if (gridRenderer != null && estado.marcadores != null)
        {
            gridRenderer.RenderSmoke(estado.marcadores.humo);
        }
        if (gridRenderer != null && estado.bomberos != null)
        {
            gridRenderer.RenderFirefighters(estado.bomberos);
        }
        if (gridRenderer != null && estado.marcadores != null)
        {
            gridRenderer.RenderPOIs(estado.marcadores.pois);
            gridRenderer.RenderVictimFound(estado.marcadores.victimasEncontradas);
        }
        gridRenderer.puertasCerradas.Clear();
        gridRenderer.puertasAbiertas.Clear();

        foreach (var p in estado.marcadores.puertasCerradas)
            gridRenderer.puertasCerradas.Add((p.x, p.y, 0));

        foreach (var p in estado.marcadores.puertasAbiertas)
            gridRenderer.puertasAbiertas.Add((p.x, p.y, 0));

        if (gridRenderer != null && estado.grid != null)
        {
            gridRenderer.RenderWalls(estado.grid, estado.dimensiones.alto);
            gridRenderer.RenderDoors(estado.grid, estado.dimensiones.alto);
        }
    }

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

        if (stats.juegoTerminado)
        {
            Debug.Log($"¡JUEGO TERMINADO! Resultado: {stats.resultado}");
        }
    }

    void OnGUI()
    {
        // Panel de controles
        GUI.Box(new Rect(10, 10, 320, 220), "FLASH POINT - CONTROLES");

        GUILayout.BeginArea(new Rect(20, 40, 300, 180));

        GUILayout.Label("=== CONTROLES ===", titleStyle);
        GUILayout.Label("S - Inicializar simulación", labelStyle);
        GUILayout.Label("SPACE - Ejecutar step", labelStyle);
        GUILayout.Label("R - Reiniciar simulación", labelStyle);
        GUILayout.Label("O - Toggle auto-run", labelStyle);
        GUILayout.Label("P - Pausar auto-run", labelStyle);
        GUILayout.Label("I - Toggle info detallada", labelStyle);
        GUILayout.Label("J - Toggle JSON raw", labelStyle);

        GUILayout.Space(10);

        if (estadoActual != null && estadoActual.estadisticas != null)
        {
            GUILayout.Label($"Estado: {(simulationActive ? "ACTIVO" : "INACTIVO")}", labelStyle);
            GUILayout.Label($"Auto-run: {(autoRunActive ? "ON" : "OFF")}", labelStyle);
            GUILayout.Label($"Turno: {estadoActual.estadisticas.turno}", labelStyle);
        }

        GUILayout.EndArea();

        // Panel de estadísticas detalladas
        if (showDetailedInfo && estadoActual != null)
        {
            MostrarPanelEstadisticas();
        }

        // Panel de JSON raw
        if (showRawJSON && !string.IsNullOrEmpty(lastRawJSON))
        {
            MostrarPanelJSON();
        }
    }

    void MostrarPanelEstadisticas()
    {
        var stats = estadoActual.estadisticas;
        if (stats == null) return;

        GUI.Box(new Rect(Screen.width - 400, 10, 390, 500), "INFORMACIÓN DETALLADA");

        GUILayout.BeginArea(new Rect(Screen.width - 390, 40, 370, 450));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(370), GUILayout.Height(450));

        GUILayout.Label("=== ESTADO DEL JUEGO ===", titleStyle);
        GUILayout.Label($"Turno: {stats.turno}", labelStyle);
        GUILayout.Label($"Paso: {estadoActual.paso}", labelStyle);
        GUILayout.Label($"Timestamp: {estadoActual.timestamp}", labelStyle);
        GUILayout.Label($"Dimensiones: {estadoActual.dimensiones?.ancho ?? 0}x{estadoActual.dimensiones?.alto ?? 0}", labelStyle);
        GUILayout.Label($"Juego terminado: {(stats.juegoTerminado ? "SÍ" : "NO")}", labelStyle);

        if (stats.juegoTerminado && !string.IsNullOrEmpty(stats.resultado))
        {
            GUIStyle resultStyle = new GUIStyle(labelStyle);
            resultStyle.normal.textColor = Color.yellow;
            GUILayout.Label($"Resultado: {stats.resultado}", resultStyle);
        }

        GUILayout.Space(10);
        GUILayout.Label("=== VÍCTIMAS ===", titleStyle);
        GUILayout.Label($"Rescatadas: {stats.victimasRescatadas} / {stats.victimasParaGanar}", labelStyle);
        GUILayout.Label($"Perdidas: {stats.victimasPerdidas} / {stats.maxVictimasPerdidas}", labelStyle);
        GUILayout.Label($"Encontradas: {stats.victimasEncontradas}", labelStyle);

        GUILayout.Space(10);
        GUILayout.Label("=== PELIGROS ===", titleStyle);
        GUILayout.Label($"Puntos daño: {stats.puntosDano} / {stats.maxPuntosDano}", labelStyle);
        GUILayout.Label($"Fuegos activos: {stats.totalFuegosActivos}", labelStyle);
        GUILayout.Label($"Humos activos: {stats.totalHumosActivos}", labelStyle);
        GUILayout.Label($"POIs restantes: {stats.totalPoisActivos}", labelStyle);

        // Información de bomberos
        if (estadoActual.bomberos != null && estadoActual.bomberos.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("=== BOMBEROS ===", titleStyle);
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

        // Información de marcadores
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

    void MostrarPanelJSON()
    {
        GUI.Box(new Rect(10, 250, Screen.width - 20, Screen.height - 260), "JSON RAW");

        GUILayout.BeginArea(new Rect(20, 280, Screen.width - 40, Screen.height - 290));

        Vector2 jsonScrollPosition = GUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(Screen.width - 40), GUILayout.Height(Screen.height - 290));
        GUILayout.Label(FormatJSON(lastRawJSON), jsonStyle);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    string FormatJSON(string json)
    {
        // Formateo básico del JSON para mejor legibilidad
        if (string.IsNullOrEmpty(json)) return "";

        try
        {
            // Formateo simple añadiendo saltos de línea después de comas y llaves
            return json.Replace(",", ",\n")
                      .Replace("{", "{\n")
                      .Replace("}", "\n}")
                      .Replace("[", "[\n")
                      .Replace("]", "\n]");
        }
        catch
        {
            return json; // Devolver original si hay error en formateo
        }
    }
}