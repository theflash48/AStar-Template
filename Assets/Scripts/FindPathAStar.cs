using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class PathMarker
{
    public MapLocation location;
    public float G;
    public float H;
    public float F;
    public GameObject marker;
    public PathMarker parent;


    public PathMarker(MapLocation l, float g, float h, GameObject marker, PathMarker p)
    {
        location = l;
        G = g;
        H = h;
        F = g + h;
        this.marker = marker;
        parent = p;
    }
    public PathMarker(MapLocation l, float g, float h, float f, GameObject marker, PathMarker p)
    {
        location = l;
        G = g;
        H = h;
        F = f;
        this.marker = marker;
        parent = p;
    }

    public override bool Equals(object obj)
    {
        if ((obj == null) || !this.GetType().Equals(obj.GetType()))
        {
            return false;
        }
        else
        {
            return location.Equals(((PathMarker)obj).location);
        }
    }

    public override int GetHashCode()
    {
        return 0;
    }
}


public class FindPathAStar : MonoBehaviour
{
    public Maze maze;
    public Material closedMaterial;
    public Material openMaterial;

    List<PathMarker> open = new List<PathMarker>();   //Lista OPEN de PathMakers
    List<PathMarker> closed = new List<PathMarker>(); //Lista CLOSED de PathMakers

    public GameObject start;
    public GameObject end;
    public GameObject pathP;

    public float updateFreq = 0.2f;

    PathMarker goalNode;  //PathMaker que marca la posicion final (DESTINO)
    PathMarker startNode; //PathMaker que marca la posicion inicial (ORIGEN)

    PathMarker lastPos; //Nodo en el que estamos en cada momento, cuando estamos
                        //buscando el DESTINO y cuando estamos dibujando el CAMINO
    bool done = false;  // La busqueda ha terminado, con o sin solucion.
    bool pathFound = false; // Solo podemos mostrar la ruta si se encontro el destino.

    bool algorithmStarted = false;  //Hemos inicializado el juego

    void RemoveAllMarkers()
    {
        //TODO: Eliminar todos los GameObjects que puedan haber en el mapa (maze)
        GameObject[] markers = GameObject.FindGameObjectsWithTag("marker");
        foreach (GameObject m in markers)
            Destroy(m);
    }

    void BeginSearch()
    {
        // Si habia una animacion activa, la detenemos antes de reiniciar.
        CancelInvoke(nameof(Search));
        if (maze == null || maze.map == null)
        {
            Debug.LogWarning("A*: El laberinto todavia no esta preparado.");
            return;
        }

        //Aquí inicializamos las posiciones inicial, final y las listas OPEN y CLOSED

        //Esta variable controlará si el problema está solucionado
        done = false;
        pathFound = false;
        algorithmStarted = false;
        //Eliminar posibles GameObjects en el mapa
        RemoveAllMarkers();

        algorithmStarted = true;

        //Creamos una lista de localizaciones validas, esto es, objetos del tipo MapLocation
        List<MapLocation> locations = new List<MapLocation>();
        //Ahora generamos el grafo comprobando si, en cada posición, tenemos un muro o no (-1 o 1)
        // En el caso de no tener un muro, agregamos un nuevo MapLocation con esa coordenada a la lista
        for (int z = 1; z < maze.depth - 1; z++)
            for (int x = 1; x < maze.width - 1; x++)
            {
                if (maze.map[x, z] != 1)
                    locations.Add(new MapLocation(x, z));
            }
        //Las listas tienen un método Shuffle que nos permite barajar los elementos que contiene
        // Barajamos las posiciones para que no estén contiguas
        if (locations.Count < 2)
        {
            Debug.LogWarning("A*: No hay suficientes casillas libres para iniciar la busqueda.");
            return;
        }
        locations.Shuffle();

        //Escogemos una localización al azar (la que se quedó en la primera posición, por ejemplo) y esa será 
        // la posición de salida, almacenada en un Vector3
        // Cuidado: Para mantener la ubicación consistente, tenemos que multiplicar la coordenada por la escala del laberinto (maze.scale)
        Vector3 startLocation = new Vector3(locations[0].x * maze.scale, 0, locations[0].z * maze.scale);
        //Ahora podemos crear un nuevo PathMarker, el objeto que utitlizaremos para representar una posición dentro del laberinto de forma visual
        //Un PathMarker necesita:
        // - MapLocation
        // - Los valores de la función f:
        //      - g(n)
        //      - h(n)
        // - El GameObject con el que representaremos esta posición
        // - Y otro PathMarker que será el "padre" de este nodo

        //Inicializamos goalNode como posicion de llegada, creando de la misma forma un Vector3 con las coordenadas, 
        // y después un PathMarker con esta información
        Vector3 goalLocation = new Vector3(locations[1].x * maze.scale, 0, locations[1].z * maze.scale);
        goalNode = new PathMarker(new MapLocation(locations[1].x, locations[1].z), 0, 0, 0, Instantiate(end, goalLocation, Quaternion.identity), null);

        // Este nodo empieza con su valor de la heurística (distancia euclidea hasta el objetivo).
        float h_n = Vector2.Distance(locations[0].ToVector(), goalNode.location.ToVector());
        GameObject startBlock = Instantiate(start, startLocation, Quaternion.identity);

        startNode = new PathMarker(locations[0], 0, h_n, startBlock, null);

        UpdateTexts(startNode);


        //Eliminamos lo que contenga las listas de OPEN y CLOSED por si se ha vuelto a ejecutar el algoritmo
        open.Clear();
        closed.Clear();

        //Comenzamos añadiendo el nodo startNode a CLOSED
        closed.Add(startNode);

        //Y para cuestiones relacionadas con la UI, almacenamos una referencia al nodo inicial en lastPos, por ahora
        lastPos = startNode;
        algorithmStarted = true;
        Debug.Log("A*: Inicio " + startNode.location.x + "," + startNode.location.z +
                  " | Destino " + goalNode.location.x + "," + goalNode.location.z);
    }

    // Ejecuta una unica expansion de A*. El siguiente nodo se conserva en lastPos
    // para permitir tanto el modo manual (C) como la animacion (A).
    void Search(PathMarker thisNode)
    {
        if (!algorithmStarted || done || thisNode == null)
            return;

        // El origen ya pertenece a CLOSED tras BeginSearch. Al expandir un nodo
        // se examinan sus cuatro vecinos ortogonales del laberinto.
        if (thisNode.location.Equals(goalNode.location))
        {
            FinishSearch(true);
            return;
        }

        foreach (MapLocation direction in maze.directions)
        {
            MapLocation neighbour = thisNode.location + direction;

            // IMPORTANTE: comprobar limites antes de acceder a maze.map[x,z].
            if (neighbour.x <= 0 || neighbour.x >= maze.width - 1 ||
                neighbour.z <= 0 || neighbour.z >= maze.depth - 1)
                continue;

            if (maze.map[neighbour.x, neighbour.z] == 1 || IsClosed(neighbour))
                continue;

            // Coste real desde el inicio a traves del nodo actual.
            // Como solo hay cuatro direcciones, cada movimiento cuesta 1.
            float g = thisNode.G + Vector2.Distance(thisNode.location.ToVector(), neighbour.ToVector());
            // Heuristica euclidea (admisible y consistente para movimientos unitarios).
            float h = Vector2.Distance(neighbour.ToVector(), goalNode.location.ToVector());
            float f = g + h;

            // Si ya esta en OPEN, conservamos el camino de menor coste.
            // No duplicamos su marcador visual.
            if (UpdateMarker(neighbour, g, h, f, thisNode))
                continue;

            Vector3 position = new Vector3(neighbour.x * maze.scale, 0,
                                           neighbour.z * maze.scale);

            // La meta ya tiene un prefab amarillo en la escena: reutilizarlo
            // evita dos cubos superpuestos en la misma posicion.
            bool isGoal = neighbour.Equals(goalNode.location);
            GameObject block = isGoal ? goalNode.marker :
                Instantiate(pathP, position, Quaternion.identity);

            PathMarker newNode = new PathMarker(neighbour, g, h, block, thisNode);
            open.Add(newNode);
            if (!isGoal)
                SetMarkerMaterial(block, openMaterial);
            UpdateTexts(newNode);
        }

        // Se agoto la frontera: no existe una ruta entre estos dos puntos.
        if (open.Count == 0)
        {
            FinishSearch(false);
            return;
        }

        // Seleccionamos el candidato con el menor F. En caso de empate,
        // preferimos el de menor H (mas proximo al objetivo estimado).
        PathMarker best = open[0];
        foreach (PathMarker candidate in open)
        {
            if (candidate.F < best.F ||
                (Mathf.Approximately(candidate.F, best.F) && candidate.H < best.H))
                best = candidate;
        }

        open.Remove(best);
        closed.Add(best);
        lastPos = best;
        SetMarkerMaterial(best.marker, closedMaterial);

        // Al sacar la meta de OPEN, su G ya es el coste del camino optimo.
        if (best.location.Equals(goalNode.location))
            FinishSearch(true);
    }

    // Devuelve true si existe un nodo en OPEN, se actualice o no.
    // Solo modificamos su padre/costes si hemos encontrado una ruta mejor.
    bool UpdateMarker(MapLocation pos, float g, float h, float f, PathMarker prt)
    {
        foreach (PathMarker p in open)
        {
            if (!p.location.Equals(pos))
                continue;

            if (g < p.G)
            {
                p.G = g;
                p.H = h;
                p.F = f;
                p.parent = prt;
                UpdateTexts(p);
            }
            return true;
        }
        return false;
    }

    // Los prefabs tienen los TextMesh como hijos G, H y F. Escribir por nombre
    // evita depender del orden concreto de los hijos del prefab.
    void UpdateTexts(PathMarker node)
    {
        if (node == null || node.marker == null)
            return;

        foreach (TextMesh label in node.marker.GetComponentsInChildren<TextMesh>())
        {
            switch (label.gameObject.name)
            {
                case "G": label.text = "g(n): " + node.G.ToString("0.00"); break;
                case "H": label.text = "h(n): " + node.H.ToString("0.00"); break;
                case "F": label.text = "f(n): " + node.F.ToString("0.00"); break;
            }
        }
    }

    void SetMarkerMaterial(GameObject block, Material material)
    {
        if (block == null || material == null)
            return;

        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    void FinishSearch(bool found)
    {
        pathFound = found;
        done = true;
        CancelInvoke(nameof(Search));
        if (found)
            Debug.Log("A*: Destino encontrado. Coste: " + lastPos.G.ToString("0.00") +
                      ". Pulsa M para mostrar la ruta optima.");
        else
            Debug.LogWarning("A*: La lista OPEN esta vacia. No hay un camino disponible.");
    }

    bool IsClosed(MapLocation marker)
    {
        //Devolvemos TRUE si marker esta en la lista de closed
        foreach (PathMarker p in closed)
        {
            if (p.location.Equals(marker)) return true;
        }
        return false;
    }


    // Start is called before the first frame update
    void Start()
    {

    }



    // Reconstruye el mejor camino siguiendo parent desde el destino al origen.
    void GetPath()
    {
        if (!algorithmStarted || !done || !pathFound || lastPos == null)
        {
            Debug.LogWarning("A*: Primero encuentra un camino con C o A; luego pulsa M.");
            return;
        }

        CancelInvoke(nameof(Search));
        RemoveAllMarkers();

        PathMarker current = lastPos;
        int pathLength = 0;
        while (current != null)
        {
            Vector3 position = new Vector3(current.location.x * maze.scale, 0,
                                           current.location.z * maze.scale);
            GameObject block = Instantiate(pathP, position, Quaternion.identity);
            SetMarkerMaterial(block, openMaterial); // ruta final morada
            UpdateTexts(new PathMarker(current.location, current.G,
                                       current.H, block, current.parent));
            pathLength++;
            current = current.parent;
        }

        Debug.Log("A*: Ruta optima dibujada, " + pathLength + " casillas (" +
                  (pathLength - 1) + " movimientos).");
    }

    void Search()
    {
        Search(lastPos);
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current == null)
            return;

        //Si presionamos la letra "P" inicializamos el juego
        if (Keyboard.current[Key.P].wasPressedThisFrame)
        {

            BeginSearch();

        }

        //Si presionamos la letra "C" calculamos el siguiente movimiento del alg. A*
        if (Keyboard.current[Key.C].wasPressedThisFrame && !done)
        {
            Search(lastPos);
        }
        //Si presionamos la letra "M" dejamos solo los pathMakers que forman el path optimo
        if (Keyboard.current[Key.M].wasPressedThisFrame)
        {
            GetPath();
        }

        //Si presionamos la tecla A, se reproduce la búsqueda a modo animación con una frecuencia dada
        if (Keyboard.current[Key.A].wasPressedThisFrame && algorithmStarted && !done)
        {
            // Impedir dos animaciones simultaneas y evitar una frecuencia no valida.
            CancelInvoke(nameof(Search));
            InvokeRepeating(nameof(Search), 0.0f, Mathf.Max(0.05f, updateFreq));
        }
    }
}