using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

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
    public List<List<CeldaData>> grid;
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

    [Header("Prefabs")]
    public GameObject doorClosedPrefab;
    public GameObject doorOpenPrefab;
    public GameObject entrancePrefab;
    public GameObject falseAlarmPrefab;
    public GameObject firePrefab;
    public GameObject firefighterPrefab;
    public GameObject floorPrefab; // Cambiar a floor individual
    public GameObject poiPrefab;
    public GameObject smokePrefab;
    public GameObject victimFoundPrefab;
    public GameObject wallPrefab;

    [Header("Estado de la Simulación")]
    public bool simulationActive = false;
    public bool autoRunActive = false;

    // Variables internas
    private EstadoSimulacion estadoActual;
    private List<GameObject> objetosSpawneados = new List<GameObject>();
    private Dictionary<int, GameObject> bomberoObjects = new Dictionary<int, GameObject>();

    // Colores para diferentes tipos de bomberos
    private Dictionary<string, Color> coloresBomberos = new Dictionary<string, Color>
    {
        {"apagafuegos", Color.red},
        {"buscador", Color.blue},
        {"salvador", Color.green},
        {"abre_puertas", Color.yellow}
    };

    // Variables para UI
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private bool showDetailedInfo = true;

    void Start()
    {
        Debug.Log("SimulationClient iniciado. Controles:");
        Debug.Log("S - Inicializar simulación");
        Debug.Log("SPACE - Ejecutar step");
        Debug.Log("R - Reiniciar simulación");
        Debug.Log("O - Toggle auto-run");
        Debug.Log("P - Pausar auto-run");
        Debug.Log("I - Toggle info detallada");

        // Configurar estilos GUI
        titleStyle = new GUIStyle();
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle();
        labelStyle.fontSize = 12;
        labelStyle.normal.textColor = Color.white;
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
                RespuestaServidor respuesta = JsonUtility.FromJson<RespuestaServidor>(www.downloadHandler.text);
                Debug.Log($"Respuesta: {respuesta.message}");

                if (respuesta.status == "success" && respuesta.estado != null)
                {
                    simulationActive = true;
                    ActualizarVisualizacion(respuesta.estado);
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
                RespuestaServidor respuesta = JsonUtility.FromJson<RespuestaServidor>(www.downloadHandler.text);

                if (respuesta.estado != null)
                {
                    Debug.Log($"Step ejecutado - Turno: {respuesta.estado.estadisticas.turno}");
                    ActualizarVisualizacion(respuesta.estado);

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
                RespuestaServidor respuesta = JsonUtility.FromJson<RespuestaServidor>(www.downloadHandler.text);
                Debug.Log($"Simulación reiniciada: {respuesta.message}");

                if (respuesta.estado != null)
                {
                    simulationActive = true;
                    autoRunActive = false;
                    ActualizarVisualizacion(respuesta.estado);
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
                RespuestaServidor respuesta = JsonUtility.FromJson<RespuestaServidor>(www.downloadHandler.text);
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

    void ActualizarVisualizacion(EstadoSimulacion estado)
    {
        if (estado == null)
        {
            Debug.LogError("Estado recibido es nulo");
            return;
        }

        estadoActual = estado;

        // Validaciones básicas
        if (estado.dimensiones == null)
        {
            Debug.LogError("Dimensiones son nulas");
            return;
        }

        if (estado.grid == null)
        {
            Debug.LogError("Grid es nulo");
            return;
        }

        if (estado.marcadores == null)
        {
            Debug.LogError("Marcadores son nulos");
            return;
        }

        Debug.Log($"=== ACTUALIZANDO VISUALIZACIÓN ===");
        Debug.Log($"Turno: {estado.estadisticas?.turno ?? 0}");
        Debug.Log($"Dimensiones: {estado.dimensiones.ancho}x{estado.dimensiones.alto}");
        Debug.Log($"Grid filas: {estado.grid.Count}");

        // Limpiar objetos anteriores
        LimpiarObjetos();

        // Generar grid y elementos estáticos
        GenerarGrid(estado);

        // Generar elementos dinámicos
        GenerarElementosDinamicos(estado);

        // Mostrar estadísticas
        if (estado.estadisticas != null)
        {
            MostrarEstadisticas(estado.estadisticas);
        }

        Debug.Log($"Total de objetos spawneados: {objetosSpawneados.Count}");
    }

    void LimpiarObjetos()
    {
        foreach (GameObject obj in objetosSpawneados)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }
        objetosSpawneados.Clear();

        foreach (var kvp in bomberoObjects)
        {
            if (kvp.Value != null)
                DestroyImmediate(kvp.Value);
        }
        bomberoObjects.Clear();
    }

    void GenerarGrid(EstadoSimulacion estado)
    {
        if (estado.grid == null || estado.grid.Count == 0)
        {
            Debug.LogError("Grid vacío o nulo");
            return;
        }

        // Generar pisos y paredes
        for (int y = 0; y < estado.dimensiones.alto; y++)
        {
            if (y >= estado.grid.Count)
            {
                Debug.LogError($"Fila {y} no existe en grid");
                continue;
            }

            for (int x = 0; x < estado.dimensiones.ancho; x++)
            {
                if (x >= estado.grid[y].Count)
                {
                    Debug.LogError($"Columna {x} no existe en fila {y}");
                    continue;
                }

                CeldaData celda = estado.grid[y][x];
                Vector3 posicion = new Vector3(x * 10f, 0, y * 10f); // Espaciado de 10 unidades

                // Generar piso base
                if (celda.esPiso && floorPrefab != null)
                {
                    GameObject floorObj = Instantiate(floorPrefab, posicion, Quaternion.identity);
                    objetosSpawneados.Add(floorObj);
                }

                // Generar paredes
                GenerarParedes(celda, posicion);
            }
        }

        // Generar elementos estáticos
        GenerarElementosEstaticos(estado);
    }

    void GenerarParedes(CeldaData celda, Vector3 posicionBase)
    {
        if (wallPrefab == null || celda.paredes == null) return;

        // [arriba, izquierda, abajo, derecha]
        Vector3[] offsetsParedes = {
            new Vector3(0, 2.5f, 5f),    // arriba
            new Vector3(-5f, 2.5f, 0),   // izquierda  
            new Vector3(0, 2.5f, -5f),   // abajo
            new Vector3(5f, 2.5f, 0)     // derecha
        };

        Vector3[] rotaciones = {
            new Vector3(0, 0, 0),      // arriba
            new Vector3(0, 90, 0),     // izquierda
            new Vector3(0, 180, 0),    // abajo
            new Vector3(0, 270, 0)     // derecha
        };

        for (int i = 0; i < celda.paredes.Length && i < 4; i++)
        {
            if (celda.paredes[i] == 1)
            {
                Vector3 posicionPared = posicionBase + offsetsParedes[i];
                Quaternion rotacionPared = Quaternion.Euler(rotaciones[i]);
                GameObject paredObj = Instantiate(wallPrefab, posicionPared, rotacionPared);
                objetosSpawneados.Add(paredObj);
            }
        }
    }

    void GenerarElementosEstaticos(EstadoSimulacion estado)
    {
        // Generar entradas
        if (estado.marcadores.entradas != null)
        {
            foreach (var entrada in estado.marcadores.entradas)
            {
                if (entrancePrefab != null)
                {
                    Vector3 pos = new Vector3(entrada.x * 10f, 1f, entrada.y * 10f);
                    GameObject entranceObj = Instantiate(entrancePrefab, pos, Quaternion.identity);
                    objetosSpawneados.Add(entranceObj);
                }
            }
        }

        // Generar puertas cerradas
        if (estado.marcadores.puertasCerradas != null)
        {
            foreach (var puerta in estado.marcadores.puertasCerradas)
            {
                if (doorClosedPrefab != null)
                {
                    Vector3 pos = new Vector3(puerta.x * 10f, 1f, puerta.y * 10f);
                    GameObject doorObj = Instantiate(doorClosedPrefab, pos, Quaternion.identity);
                    objetosSpawneados.Add(doorObj);
                }
            }
        }

        // Generar puertas abiertas
        if (estado.marcadores.puertasAbiertas != null)
        {
            foreach (var puerta in estado.marcadores.puertasAbiertas)
            {
                if (doorOpenPrefab != null)
                {
                    Vector3 pos = new Vector3(puerta.x * 10f, 1f, puerta.y * 10f);
                    GameObject doorObj = Instantiate(doorOpenPrefab, pos, Quaternion.identity);
                    objetosSpawneados.Add(doorObj);
                }
            }
        }
    }

    void GenerarElementosDinamicos(EstadoSimulacion estado)
    {
        // Generar fuego
        if (estado.marcadores.fuego != null)
        {
            foreach (var fuego in estado.marcadores.fuego)
            {
                if (firePrefab != null)
                {
                    Vector3 pos = new Vector3(fuego.x * 10f, 2f, fuego.y * 10f);
                    GameObject fireObj = Instantiate(firePrefab, pos, Quaternion.identity);
                    objetosSpawneados.Add(fireObj);
                }
            }
        }

        // Generar humo
        if (estado.marcadores.humo != null)
        {
            foreach (var humo in estado.marcadores.humo)
            {
                if (smokePrefab != null)
                {
                    Vector3 pos = new Vector3(humo.x * 10f, 3f, humo.y * 10f);
                    GameObject smokeObj = Instantiate(smokePrefab, pos, Quaternion.identity);
                    objetosSpawneados.Add(smokeObj);
                }
            }
        }

        // Generar POIs
        if (estado.marcadores.pois != null)
        {
            foreach (var poi in estado.marcadores.pois)
            {
                GameObject prefabAUsar = poi.tipo == "victima" ? poiPrefab : falseAlarmPrefab;
                if (prefabAUsar != null)
                {
                    Vector3 pos = new Vector3(poi.x * 10f, 1.5f, poi.y * 10f);
                    GameObject poiObj = Instantiate(prefabAUsar, pos, Quaternion.identity);
                    objetosSpawneados.Add(poiObj);
                }
            }
        }

        // Generar víctimas encontradas
        if (estado.marcadores.victimasEncontradas != null)
        {
            foreach (var victima in estado.marcadores.victimasEncontradas)
            {
                if (victimFoundPrefab != null)
                {
                    Vector3 pos = new Vector3(victima.x * 10f, 2.5f, victima.y * 10f);
                    GameObject victimObj = Instantiate(victimFoundPrefab, pos, Quaternion.identity);
                    objetosSpawneados.Add(victimObj);
                }
            }
        }

        // Generar bomberos
        if (estado.bomberos != null)
        {
            foreach (var bombero in estado.bomberos)
            {
                if (firefighterPrefab != null)
                {
                    Vector3 pos = new Vector3(bombero.posicion.x * 10f, 5f, bombero.posicion.y * 10f);
                    GameObject bomberoObj = Instantiate(firefighterPrefab, pos, Quaternion.identity);

                    // Cambiar color según tipo
                    if (coloresBomberos.ContainsKey(bombero.tipo))
                    {
                        Renderer renderer = bomberoObj.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material.color = coloresBomberos[bombero.tipo];
                        }
                    }

                    bomberoObjects[bombero.id] = bomberoObj;
                    objetosSpawneados.Add(bomberoObj);
                }
            }
        }
    }

    void MostrarEstadisticas(Estadisticas stats)
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
        GUI.Box(new Rect(10, 10, 320, 200), "FLASH POINT - CONTROLES");

        GUILayout.BeginArea(new Rect(20, 40, 300, 160));

        GUILayout.Label("=== CONTROLES ===", titleStyle);
        GUILayout.Label("S - Inicializar simulación", labelStyle);
        GUILayout.Label("SPACE - Ejecutar step", labelStyle);
        GUILayout.Label("R - Reiniciar simulación", labelStyle);
        GUILayout.Label("O - Toggle auto-run", labelStyle);
        GUILayout.Label("P - Pausar auto-run", labelStyle);
        GUILayout.Label("I - Toggle info detallada", labelStyle);

        GUILayout.Space(10);

        if (estadoActual != null && estadoActual.estadisticas != null)
        {
            GUILayout.Label($"Estado: {(simulationActive ? "ACTIVO" : "INACTIVO")}", labelStyle);
            GUILayout.Label($"Auto-run: {(autoRunActive ? "ON" : "OFF")}", labelStyle);
            GUILayout.Label($"Turno: {estadoActual.estadisticas.turno}", labelStyle);
        }

        GUILayout.EndArea();

        // Panel de estadísticas detalladas
        if (showDetailedInfo && estadoActual != null && estadoActual.estadisticas != null)
        {
            var stats = estadoActual.estadisticas;

            GUI.Box(new Rect(Screen.width - 350, 10, 340, 300), "ESTADÍSTICAS");

            GUILayout.BeginArea(new Rect(Screen.width - 340, 40, 320, 260));

            GUILayout.Label("=== ESTADO DEL JUEGO ===", titleStyle);
            GUILayout.Label($"Turno: {stats.turno}", labelStyle);
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

            GUILayout.EndArea();
        }
    }
}