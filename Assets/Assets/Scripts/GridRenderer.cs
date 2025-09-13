using System.Collections.Generic;
using UnityEngine;

// Clase encargada de renderizar todos los elementos visuales del grid de simulación.
public class GridRenderer : MonoBehaviour
{
    // Referencias a los prefabs que se instanciarán para representar cada elemento.
    public GameObject cellPrefab; // Prefab para las celdas del piso.
    public GameObject firePrefab; // Prefab para representar fuego.
    public GameObject smokePrefab; // Prefab para representar humo.
    public GameObject firefighterPrefab; // Prefab para representar bomberos.
    public GameObject poiPrefab; // Prefab para puntos de interés.
    public GameObject victimFoundPrefab; // Prefab para víctimas encontradas.
    public GameObject wallPrefab; // Prefab para paredes.
    public GameObject doorPrefab; // Prefab para puertas.

    // Configuración visual del grid.
    public float cellSize = 1.01f; // Tamaño de cada celda en unidades de Unity.

    // Diccionarios para almacenar referencias a los objetos instanciados y poder reutilizarlos.
    private Dictionary<Vector2Int, GameObject> cellObjects = new Dictionary<Vector2Int, GameObject>(); // Objetos de celdas indexados por posición.
    private Dictionary<Vector2Int, GameObject> fireObjects = new Dictionary<Vector2Int, GameObject>(); // Objetos de fuego indexados por posición.
    private Dictionary<Vector2Int, GameObject> smokeObjects = new Dictionary<Vector2Int, GameObject>(); // Objetos de humo indexados por posición.
    private Dictionary<int, GameObject> firefighterObjects = new Dictionary<int, GameObject>(); // Objetos de bomberos indexados por ID.
    private Dictionary<Vector2Int, GameObject> poiObjects = new Dictionary<Vector2Int, GameObject>(); // Objetos de POIs indexados por posición.
    private Dictionary<Vector2Int, GameObject> victimFoundObjects = new Dictionary<Vector2Int, GameObject>(); // Objetos de víctimas encontradas indexados por posición.
    private Dictionary<(int, int, int), GameObject> wallObjects = new Dictionary<(int, int, int), GameObject>(); // Objetos de paredes indexados por (x, y, dirección).
    private Dictionary<(int x, int y, int dir), GameObject> doorObjects = new Dictionary<(int, int, int), GameObject>(); // Objetos de puertas indexados por (x, y, dirección).

    // Sets para controlar el estado de las puertas, llenados desde SimulationClient.
    public HashSet<(int x, int y, int dir)> puertasCerradas = new HashSet<(int, int, int)>(); // Posiciones de puertas cerradas.
    public HashSet<(int x, int y, int dir)> puertasAbiertas = new HashSet<(int, int, int)>(); // Posiciones de puertas abiertas.

    private int gridHeight; // Altura del grid para calcular inversión de coordenadas Y.

    // Renderiza el grid base con todas las celdas del juego.
    public void RenderGrid(List<CeldaData> grid, int ancho, int alto)
    {
        gridHeight = alto; // Guardar altura para conversiones de coordenadas.

        // Limpiar celdas anteriores para evitar acumulación.
        foreach (var obj in cellObjects.Values) Destroy(obj);
        cellObjects.Clear();

        // Limpiar fuegos anteriores por compatibilidad.
        foreach (var obj in fireObjects.Values) Destroy(obj);
        fireObjects.Clear();

        // Instanciar una celda por cada dato recibido.
        foreach (var celda in grid)
        {
            // Calcular posición invirtiendo Y para coincidir con sistema de coordenadas del servidor.
            Vector3 pos = new Vector3(celda.x * cellSize, 0, (alto - 1 - celda.y) * cellSize);
            GameObject cellObj = Instantiate(cellPrefab, pos, Quaternion.identity, transform);

            // Colorear celdas especiales como entradas.
            Renderer rend = cellObj.GetComponent<Renderer>();
            if (rend != null)
            {
                if (celda.esEntrada) rend.material.color = Color.gray; // Marcar entradas en gris.
            }

            // Almacenar referencia para futuras actualizaciones.
            cellObjects[new Vector2Int(celda.x, celda.y)] = cellObj;
        }

        Debug.Log($"Grid renderizado: {grid.Count} celdas");
    }

    // Renderiza todos los fuegos activos en el grid.
    public void RenderFire(List<MarcadorPosicion> fuegos)
    {
        // Limpiar fuegos anteriores para evitar duplicados.
        foreach (var obj in fireObjects.Values) Destroy(obj);
        fireObjects.Clear();

        if (fuegos == null) return; // Salir si no hay fuegos que renderizar.

        // Instanciar un objeto de fuego por cada posición.
        foreach (var f in fuegos)
        {
            // Posicionar ligeramente arriba del piso y con Y invertida.
            Vector3 pos = new Vector3(f.x * cellSize, 0.15f, (gridHeight - 1 - f.y) * cellSize);
            GameObject fireObj = Instantiate(firePrefab, pos, Quaternion.identity, transform);
            fireObjects[new Vector2Int(f.x, f.y)] = fireObj;
        }

        Debug.Log($"Fuegos renderizados: {fuegos.Count}");
    }

    // Renderiza todo el humo activo en el grid.
    public void RenderSmoke(List<MarcadorPosicion> humos)
    {
        // Limpiar humo anterior para evitar acumulación.
        foreach (var obj in smokeObjects.Values) Destroy(obj);
        smokeObjects.Clear();

        if (humos == null) return; // Salir si no hay humo que renderizar.

        // Instanciar un objeto de humo por cada posición.
        foreach (var h in humos)
        {
            // Posicionar ligeramente arriba del piso con Y invertida.
            Vector3 pos = new Vector3(h.x * cellSize, 0.15f, (gridHeight - 1 - h.y) * cellSize);
            GameObject smokeObj = Instantiate(smokePrefab, pos, Quaternion.identity, transform);
            smokeObjects[new Vector2Int(h.x, h.y)] = smokeObj;
        }

        Debug.Log($"Humos renderizados: {humos.Count}");
    }

    // Renderiza y actualiza las posiciones de todos los bomberos.
    public void RenderFirefighters(List<Bombero> bomberos)
    {
        if (bomberos == null) return; // Salir si no hay bomberos.

        // Ocultar todos los bomberos existentes primero.
        foreach (var go in firefighterObjects.Values)
        {
            go.SetActive(false);
        }

        // Procesar cada bombero recibido del servidor.
        foreach (var b in bomberos)
        {
            // Calcular posición más alta que otros elementos y con Y invertida.
            Vector3 pos = new Vector3(b.posicion.x * cellSize, 0.25f, (gridHeight - 1 - b.posicion.y) * cellSize);

            if (!firefighterObjects.ContainsKey(b.id))
            {
                // Instanciar bombero nuevo si no existe.
                GameObject ffObj = Instantiate(firefighterPrefab, pos, Quaternion.identity, transform);
                firefighterObjects[b.id] = ffObj;
            }
            else
            {
                // Reposicionar bombero existente y reactivarlo.
                GameObject ffObj = firefighterObjects[b.id];
                ffObj.transform.position = pos;
                ffObj.SetActive(true);
            }
        }
    }

    // Renderiza todos los puntos de interés (POIs) activos.
    public void RenderPOIs(List<MarcadorPosicion> pois)
    {
        // Limpiar POIs anteriores.
        foreach (var obj in poiObjects.Values) Destroy(obj);
        poiObjects.Clear();

        if (pois == null) return; // Salir si no hay POIs.

        // Instanciar un POI por cada posición.
        foreach (var p in pois)
        {
            // Posicionar a altura intermedia con Y invertida.
            Vector3 pos = new Vector3(p.x * cellSize, 0.125f, (gridHeight - 1 - p.y) * cellSize);
            GameObject poiObj = Instantiate(poiPrefab, pos, Quaternion.identity, transform);
            poiObjects[new Vector2Int(p.x, p.y)] = poiObj;
        }
    }

    // Renderiza todas las víctimas que han sido encontradas.
    public void RenderVictimFound(List<MarcadorPosicion> victims)
    {
        // Limpiar víctimas anteriores.
        foreach (var obj in victimFoundObjects.Values) Destroy(obj);
        victimFoundObjects.Clear();

        if (victims == null) return; // Salir si no hay víctimas encontradas.

        // Instanciar una víctima por cada posición.
        foreach (var v in victims)
        {
            // Posicionar a altura intermedia con Y invertida.
            Vector3 pos = new Vector3(v.x * cellSize, 0.125f, (gridHeight - 1 - v.y) * cellSize);
            GameObject victimObj = Instantiate(victimFoundPrefab, pos, Quaternion.identity, transform);
            victimFoundObjects[new Vector2Int(v.x, v.y)] = victimObj;
        }
    }

    // Renderiza todas las paredes del grid basándose en los datos de cada celda.
    public void RenderWalls(List<CeldaData> grid, int gridHeight)
    {
        // Limpiar paredes anteriores.
        foreach (var obj in wallObjects.Values) Destroy(obj);
        wallObjects.Clear();

        if (grid == null) return; // Salir si no hay datos del grid.

        // Procesar cada celda para buscar paredes.
        foreach (var celda in grid)
        {
            // Calcular posición base de la celda con Y invertida.
            float zPosBase = (gridHeight - 1 - celda.y) * cellSize;
            Vector3 cellPos = new Vector3(celda.x * cellSize, 0, zPosBase);

            // Revisar las 4 direcciones posibles (arriba, izquierda, abajo, derecha).
            for (int i = 0; i < 4; i++)
            {
                if (celda.paredes[i] == 1) // Si hay pared en esta dirección.
                {
                    // Posicionar pared a media altura del prefab.
                    Vector3 pos = cellPos + new Vector3(0, wallPrefab.transform.localScale.y / 2, 0);
                    Quaternion rot = Quaternion.identity;

                    // Calcular offset y rotación según la dirección de la pared.
                    switch (i)
                    {
                        case 0: pos += new Vector3(0, 0, cellSize / 2); rot = Quaternion.identity; break; // Arriba.
                        case 1: pos += new Vector3(-cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break; // Izquierda.
                        case 2: pos += new Vector3(0, 0, -cellSize / 2); rot = Quaternion.identity; break; // Abajo.
                        case 3: pos += new Vector3(cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break; // Derecha.
                    }

                    // Instanciar pared y almacenar referencia.
                    GameObject wallObj = Instantiate(wallPrefab, pos, rot, transform);
                    wallObjects[(celda.x, celda.y, i)] = wallObj;
                }
            }
        }
    }

    // Renderiza todas las puertas (abiertas y cerradas) basándose en los HashSets.
    public void RenderDoors(List<CeldaData> grid, int gridHeight)
    {
        // Limpiar puertas anteriores.
        foreach (var obj in doorObjects.Values) Destroy(obj);
        doorObjects.Clear();

        if (grid == null) return; // Salir si no hay datos del grid.

        // Procesar cada celda para buscar puertas.
        foreach (var celda in grid)
        {
            // Calcular posición base de la celda con Y invertida.
            float zBase = (gridHeight - 1 - celda.y) * cellSize;
            float yBase = doorPrefab.transform.localScale.y / 2; // Altura media del prefab.
            Vector3 cellPos = new Vector3(celda.x * cellSize, yBase, zBase);

            // Revisar las 4 direcciones posibles.
            for (int i = 0; i < 4; i++)
            {
                bool generar = false;
                bool esCerrada = false;

                // Verificar si hay puerta cerrada en esta posición y dirección.
                if (puertasCerradas.Contains((celda.x, celda.y, i)))
                {
                    generar = true;
                    esCerrada = true;
                }
                // Verificar si hay puerta abierta en esta posición y dirección.
                else if (puertasAbiertas.Contains((celda.x, celda.y, i)))
                {
                    generar = true;
                    esCerrada = false;
                }

                if (!generar) continue; // Saltar si no hay puerta en esta dirección.

                Vector3 offset = Vector3.zero;
                Quaternion rot = Quaternion.identity;

                // Calcular offset y rotación según la dirección de la puerta.
                switch (i)
                {
                    case 0: offset = new Vector3(0, 0, cellSize / 2); break; // Arriba.
                    case 1: offset = new Vector3(-cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break; // Izquierda.
                    case 2: offset = new Vector3(0, 0, -cellSize / 2); break; // Abajo.
                    case 3: offset = new Vector3(cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break; // Derecha.
                }

                // Instanciar puerta en la posición calculada.
                Vector3 doorPos = cellPos + offset;
                GameObject doorObj = Instantiate(doorPrefab, doorPos, rot, transform);

                // Cambiar color según el estado de la puerta.
                Renderer rend = doorObj.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = esCerrada ? Color.brown : Color.gray; // Marrón para cerradas, gris para abiertas.

                // Almacenar referencia de la puerta.
                doorObjects[(celda.x, celda.y, i)] = doorObj;
            }
        }
    }
}