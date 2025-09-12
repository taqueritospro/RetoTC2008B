using System.Collections.Generic;
using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject cellPrefab;
    public GameObject firePrefab;
    public GameObject smokePrefab;
    public GameObject firefighterPrefab;
    public GameObject poiPrefab;
    public GameObject victimFoundPrefab;
    public GameObject wallPrefab;
    public GameObject doorPrefab;


    [Header("Configuración del grid")]
    public float cellSize = 1.01f;

    private Dictionary<Vector2Int, GameObject> cellObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> fireObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> smokeObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<int, GameObject> firefighterObjects = new Dictionary<int, GameObject>();
    private Dictionary<Vector2Int, GameObject> poiObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> victimFoundObjects = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<(int, int, int), GameObject> wallObjects = new Dictionary<(int, int, int), GameObject>();
    private Dictionary<(int x, int y, int dir), GameObject> doorObjects = new Dictionary<(int, int, int), GameObject>();

    // HashSet para controlar qué puertas están cerradas o abiertas (puedes llenar esto desde SimulationClient)
    public HashSet<(int x, int y, int dir)> puertasCerradas = new HashSet<(int, int, int)>();
    public HashSet<(int x, int y, int dir)> puertasAbiertas = new HashSet<(int, int, int)>();

    public void RenderGrid(List<CeldaData> grid, int ancho, int alto)
    {
        // limpiar celdas anteriores.
        foreach (var obj in cellObjects.Values) Destroy(obj);
        cellObjects.Clear();

        // limpiar fuegos anteriores.
        foreach (var obj in fireObjects.Values) Destroy(obj);
        fireObjects.Clear();

        // Instanciar celdas.
        foreach (var celda in grid)
        {
            Vector3 pos = new Vector3(celda.x * cellSize, 0, celda.y * cellSize);
            GameObject cellObj = Instantiate(cellPrefab, pos, Quaternion.identity, transform);

            Renderer rend = cellObj.GetComponent<Renderer>();
            if (rend != null)
            {
                // Colorear el piso, aunque el fuego se representará aparte.
                if (celda.esEntrada) rend.material.color = Color.gray;
            }

            cellObjects[new Vector2Int(celda.x, celda.y)] = cellObj;
        }

        Debug.Log($"Grid renderizado: {grid.Count} celdas");
    }

    public void RenderFire(List<MarcadorPosicion> fuegos)
    {
        // limpiar fuegos anteriores.
        foreach (var obj in fireObjects.Values) Destroy(obj);
        fireObjects.Clear();

        if (fuegos == null) return;

        foreach (var f in fuegos)
        {
            Vector3 pos = new Vector3(f.x * cellSize, 0.15f, f.y * cellSize); // un poco más alto que el piso
            GameObject fireObj = Instantiate(firePrefab, pos, Quaternion.identity, transform);
            fireObjects[new Vector2Int(f.x, f.y)] = fireObj;
        }

        Debug.Log($"Fuegos renderizados: {fuegos.Count}");
    }

    public void RenderSmoke(List<MarcadorPosicion> humos)
    {
        // limpiar humo anterior.
        foreach (var obj in smokeObjects.Values) Destroy(obj);
        smokeObjects.Clear();

        if (humos == null) return;

        foreach (var h in humos)
        {
            Vector3 pos = new Vector3(h.x * cellSize, 0.15f, h.y * cellSize);
            GameObject smokeObj = Instantiate(smokePrefab, pos, Quaternion.identity, transform);
            smokeObjects[new Vector2Int(h.x, h.y)] = smokeObj;
        }

        Debug.Log($"Humos renderizados: {humos.Count}");
    }

    public void RenderFirefighters(List<Bombero> bomberos)
    {
        if (bomberos == null) return;

        // Ocultar todos los existentes primero.
        foreach (var go in firefighterObjects.Values)
        {
            go.SetActive(false);
        }

        foreach (var b in bomberos)
        {
            Vector3 pos = new Vector3(b.posicion.x * cellSize, 0.25f, b.posicion.y * cellSize);

            if (!firefighterObjects.ContainsKey(b.id))
            {
                // Instanciar bombero nuevo.
                GameObject ffObj = Instantiate(firefighterPrefab, pos, Quaternion.identity, transform);
                firefighterObjects[b.id] = ffObj;
            }
            else
            {
                // Reposicionar el bombero existente.
                GameObject ffObj = firefighterObjects[b.id];
                ffObj.transform.position = pos;
                ffObj.SetActive(true);
            }
        }
    }
    public void RenderPOIs(List<MarcadorPosicion> pois)
    {
        foreach (var obj in poiObjects.Values) Destroy(obj);
        poiObjects.Clear();

        if (pois == null) return;

        foreach (var p in pois)
        {
            Vector3 pos = new Vector3(p.x * cellSize, 0.125f, p.y * cellSize);
            GameObject poiObj = Instantiate(poiPrefab, pos, Quaternion.identity, transform);
            poiObjects[new Vector2Int(p.x, p.y)] = poiObj;
        }
    }

    public void RenderVictimFound(List<MarcadorPosicion> victims)
    {
        foreach (var obj in victimFoundObjects.Values) Destroy(obj);
        victimFoundObjects.Clear();

        if (victims == null) return;

        foreach (var v in victims)
        {
            Vector3 pos = new Vector3(v.x * cellSize, 0.125f, v.y * cellSize);
            GameObject victimObj = Instantiate(victimFoundPrefab, pos, Quaternion.identity, transform);
            victimFoundObjects[new Vector2Int(v.x, v.y)] = victimObj;
        }
    }
    public void RenderWalls(List<CeldaData> grid, int gridHeight)
    {
        foreach (var obj in wallObjects.Values) Destroy(obj);
        wallObjects.Clear();

        if (grid == null) return;

        foreach (var celda in grid)
        {
            float zPosBase = (gridHeight - 1 - celda.y) * cellSize;
            Vector3 cellPos = new Vector3(celda.x * cellSize, 0, zPosBase);

            for (int i = 0; i < 4; i++)
            {
                if (celda.paredes[i] == 1)
                {
                    Vector3 pos = cellPos + new Vector3(0, wallPrefab.transform.localScale.y / 2, 0);
                    Quaternion rot = Quaternion.identity;

                    switch (i)
                    {
                        case 0: pos += new Vector3(0, 0, cellSize / 2); rot = Quaternion.identity; break;
                        case 1: pos += new Vector3(-cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break;
                        case 2: pos += new Vector3(0, 0, -cellSize / 2); rot = Quaternion.identity; break;
                        case 3: pos += new Vector3(cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break;
                    }

                    GameObject wallObj = Instantiate(wallPrefab, pos, rot, transform);
                    wallObjects[(celda.x, celda.y, i)] = wallObj;
                }
            }
        }
    }
    public void RenderDoors(List<CeldaData> grid, int gridHeight)
    {
        // Limpiar puertas anteriores
        foreach (var obj in doorObjects.Values) Destroy(obj);
        doorObjects.Clear();

        if (grid == null) return;

        foreach (var celda in grid)
        {
            float zBase = (gridHeight - 1 - celda.y) * cellSize;
            float yBase = doorPrefab.transform.localScale.y / 2; // mitad altura
            Vector3 cellPos = new Vector3(celda.x * cellSize, yBase, zBase);

            for (int i = 0; i < 4; i++)
            {
                bool generar = false;
                bool esCerrada = false;

                if (puertasCerradas.Contains((celda.x, celda.y, i)))
                {
                    generar = true;
                    esCerrada = true;
                }
                else if (puertasAbiertas.Contains((celda.x, celda.y, i)))
                {
                    generar = true;
                    esCerrada = false;
                }

                if (!generar) continue;

                Vector3 offset = Vector3.zero;
                Quaternion rot = Quaternion.identity;

                switch (i)
                {
                    case 0: offset = new Vector3(0, 0, cellSize / 2); break; // Arriba
                    case 1: offset = new Vector3(-cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break; // Izquierda
                    case 2: offset = new Vector3(0, 0, -cellSize / 2); break; // Abajo
                    case 3: offset = new Vector3(cellSize / 2, 0, 0); rot = Quaternion.Euler(0, 90, 0); break; // Derecha
                }

                Vector3 doorPos = cellPos + offset;
                GameObject doorObj = Instantiate(doorPrefab, doorPos, rot, transform);

                // Cambiar color/material según estado
                Renderer rend = doorObj.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = esCerrada ? Color.brown : Color.gray3;

                doorObjects[(celda.x, celda.y, i)] = doorObj;
            }
        }
    }

}
