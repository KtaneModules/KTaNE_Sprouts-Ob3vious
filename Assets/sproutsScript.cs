using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using System.Text.RegularExpressions;

public class sproutsScript : MonoBehaviour
{

    //public stuff
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public Transform[] Objects;
    public KMBombModule Module;

    //functionality
    private bool solved = false;
    private bool handleBreakdown = false;
    private bool calculating = true;

    private List<bool> isHelper = new List<bool> { };
    private List<Transform> edges = new List<Transform> { };
    private List<Transform> vertices = new List<Transform> { };
    private List<int[]> connections = new List<int[]> { };
    private List<MeshRenderer> tempJunk = new List<MeshRenderer> { };

    private List<Connection> validpaths = new List<Connection> { };
    private int index = 0;
    private readonly int DotCount = 3;

    //To store connection systems easily
    private struct Connection
    {
        public Connection(int a, int b, List<float[]> p)
        {
            Start = a;
            End = b;
            Path = p;
        }
        public int Start { get; }
        public int End { get; }
        public List<float[]> Path { get; set; }

        public override string ToString()
        {
            return Path.Select(x => "[" + x.Join(", ") + "]").Join();
        }
    }

    //internals
    private static bool _isUsingThreads;
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    void Awake()
    {
        for (int i = 0; i < 2; i++)
        {
            int x = i;
            Buttons[i].OnInteract += delegate
            {
                if (!solved && !calculating)
                {
                    Buttons[x].AddInteractionPunch();
                    Audio.PlaySoundAtTransform("press", Module.transform);
                    switch (x)
                    {
                        case 0:
                            DrawPath(validpaths[index], true, 0);
                            index = 0;
                            calculating = true;
                            StartCoroutine(Calculate(true));
                            break;
                        case 1:
                            index++;
                            index %= validpaths.Count();
                            DrawPath(validpaths[index], false, 0);
                            break;
                    }
                }
                return false;
            };
            Buttons[i].OnHighlight += delegate
            {
                if (!solved && !calculating)
                    Buttons[x].GetComponent<MeshRenderer>().material.color = new Color32(51, 51, 51, 255);
            };
            Buttons[i].OnHighlightEnded += delegate
            {
                Buttons[x].GetComponent<MeshRenderer>().material.color = new Color32(34, 34, 34, 255);
            };
        }
    }

    void Start()
    {
        _isUsingThreads = false;
        Generate(DotCount);
    }

    private void Generate(int n)
    {
        foreach (var item in vertices)
            item.GetComponent<MeshRenderer>().enabled = false;
        foreach (var item in edges)
            item.GetComponent<MeshRenderer>().enabled = false;
        vertices = new List<Transform> { };
        isHelper = new List<bool> { };
        edges = new List<Transform> { };
        connections = new List<int[]> { };
        for (int i = 0; i < 3; i++)
            Objects[i].GetComponent<MeshRenderer>().enabled = true;
        for (int i = 0; i < n; i++)
        {
            vertices.Add(Instantiate(Objects[0], Module.transform));
            vertices[vertices.Count - 1].localPosition = new Vector3(Rnd.Range(-0.065f, 0.025f), 0.015f, Rnd.Range(-0.065f, 0.065f));
            while (vertices.Take(vertices.Count - 1).Any(x => Pytha(x.localPosition.x - vertices[vertices.Count - 1].localPosition.x, x.localPosition.z - vertices[vertices.Count - 1].localPosition.z) < 0.05f))
                vertices[vertices.Count - 1].localPosition = new Vector3(Rnd.Range(-0.065f, 0.025f), 0.015f, Rnd.Range(-0.065f, 0.065f));
            isHelper.Add(false);
        }
        for (int i = 0; i < 3; i++)
            Objects[i].GetComponent<MeshRenderer>().enabled = false;
        StartCoroutine(Calculate(n % 6 < 3));
    }

    private IEnumerator Calculate(bool bot)
    {
        yield return new WaitForSecondsRealtime(Rnd.Range(0, 3f));
        yield return new WaitWhile(() => _isUsingThreads);
        _isUsingThreads = true;
        if (bot)
        {
            List<float[]> existingVerts = new List<float[]> { new float[] { -0.0701f, -0.0701f }, new float[] { 0.03f, -0.07f }, new float[] { -0.07f, 0.07f }, new float[] { 0.0301f, 0.0701f } }.Concat(vertices.Select(x => new float[] { x.localPosition.x, x.localPosition.z })).ToList();
            new Thread(() =>
            {
                validpaths = GatherOptions(existingVerts);
            }).Start();
            yield return new WaitWhile(() => calculating);
            yield return null;
            if (validpaths.Count == 0)
            {
                if (handleBreakdown)
                {
                    Audio.PlaySoundAtTransform("restart", Module.transform);
                    Generate(DotCount);
                }
                else
                {
                    Audio.PlaySoundAtTransform("solve", Module.transform);
                    solved = true;
                    Module.HandlePass();
                    foreach (var item in edges)
                        item.GetComponent<MeshRenderer>().material.color = new Color(0, 1, 0, 1);
                    for (int i = 0; i < vertices.Count; i++)
                        if (isHelper[i])
                            vertices[i].GetComponent<MeshRenderer>().material.color = new Color(0, 1, 0, 1);
                }
                handleBreakdown = false;
            }
            else
            {
                Audio.PlaySoundAtTransform("opponent", Module.transform);
                DrawPath(validpaths.PickRandom(), true, 1);
            }
        }
        if (!solved)
        {
            calculating = true;
            List<float[]> existingVerts = new List<float[]> { new float[] { -0.0701f, -0.0701f }, new float[] { 0.03f, -0.07f }, new float[] { -0.07f, 0.07f }, new float[] { 0.0301f, 0.0701f } }.Concat(vertices.Select(x => new float[] { x.localPosition.x, x.localPosition.z })).ToList();
            new Thread(() =>
            {
                validpaths = GatherOptions(existingVerts);
            }).Start();
            yield return new WaitWhile(() => calculating);
            if (validpaths.Count == 0)
            {
                Audio.PlaySoundAtTransform("restart", Module.transform);
                Generate(DotCount);
            }
            else
                DrawPath(validpaths[0], false, 0);
        }
        _isUsingThreads = false;
    }

    private void DrawPath(Connection p, bool s, int c)
    {
        foreach (var item in tempJunk)
            item.enabled = false;
        tempJunk = new List<MeshRenderer> { };
        List<float[]> totalPath = new float[][] { new float[] { vertices[p.Start].localPosition.x, vertices[p.Start].localPosition.z } }.Concat(p.Path).Concat(new float[][] { new float[] { vertices[p.End].localPosition.x, vertices[p.End].localPosition.z } }).ToList();
        Objects[2].GetComponent<MeshRenderer>().enabled = true;
        for (int i = 0; i < totalPath.Count - 1; i++)
        {
            float deltax = totalPath[i][0] - totalPath[i + 1][0];
            float deltay = totalPath[i][1] - totalPath[i + 1][1];
            if (s)
            {
                edges.Add(Instantiate(Objects[2], Module.transform));
                edges.Last().transform.localEulerAngles = new Vector3(0, -Mathf.Atan(deltay / deltax) * 57.2957795f, 0);
                edges.Last().transform.localScale = new Vector3(Pytha(deltax, deltay) * 50f, .1f, .1f);
                edges.Last().transform.localPosition = new Vector3((totalPath[i][0] + totalPath[i + 1][0]) / 2f, 0.015f, (totalPath[i][1] + totalPath[i + 1][1]) / 2f);
                edges.Last().GetComponent<MeshRenderer>().material.color = new Color(c, 0, 1 - c, 1);
            }
            else
            {
                tempJunk.Add(Instantiate(Objects[2].GetComponent<MeshRenderer>(), Module.transform));
                tempJunk.Last().transform.localEulerAngles = new Vector3(0, -Mathf.Atan(deltay / deltax) * 57.2957795f, 0);
                tempJunk.Last().transform.localScale = new Vector3(Pytha(deltax, deltay) * 50f, .1f, .1f);
                tempJunk.Last().transform.localPosition = new Vector3((totalPath[i][0] + totalPath[i + 1][0]) / 2f, 0.015f, (totalPath[i][1] + totalPath[i + 1][1]) / 2f);
                tempJunk.Last().material.color = new Color(0.5f, 0.5f, 0.5f, 1);
            }
        }
        Objects[2].GetComponent<MeshRenderer>().enabled = false;
        Objects[0].GetComponent<MeshRenderer>().enabled = true;
        Objects[1].GetComponent<MeshRenderer>().enabled = true;
        int index = Rnd.Range(1, totalPath.Count - 1);
        int vertexCount = vertices.Count;
        for (int i = 1; i < totalPath.Count - 1; i++)
            if (s)
            {
                isHelper.Add(i != index);
                vertices.Add(Instantiate(Objects[i == index ? 0 : 1], Module.transform));
                vertices.Last().transform.localPosition = new Vector3(totalPath[i][0], 0.015f, totalPath[i][1]);
                if (i != index)
                    vertices.Last().GetComponent<MeshRenderer>().material.color = new Color(c, 0, 1 - c, 1);
            }
            else
            {
                tempJunk.Add(Instantiate(Objects[1].GetComponent<MeshRenderer>(), Module.transform));
                tempJunk.Last().transform.localPosition = new Vector3(totalPath[i][0], 0.015f, totalPath[i][1]);
                tempJunk.Last().material.color = new Color(0.5f, 0.5f, 0.5f, 1);
            }
        Objects[0].GetComponent<MeshRenderer>().enabled = false;
        Objects[1].GetComponent<MeshRenderer>().enabled = false;
        if (s)
        {
            connections.Add(new int[] { p.Start, vertexCount });
            for (int i = 1; i < totalPath.Count - 2; i++)
                connections.Add(new int[] { vertexCount + i - 1, vertexCount + i });
            connections.Add(new int[] { p.End, vertexCount + totalPath.Count - 3 });
            Debug.Log(connections.Select(x => "[" + x.Join() + "]").Join());
            validpaths = new List<Connection> { };
        }
    }

    private List<Connection> GatherOptions(List<float[]> existingVerts)
    {
        calculating = true;
        List<int[]> lines = connections.Select(x => x.Select(y => y + 4).ToArray()).ToList();

        //triangulating
        List<int[]> triangles = Triangulate(existingVerts, lines.ToList());

        //triangle connectivity
        List<int>[] connected = new List<int>[triangles.Count];
        for (int i = 0; i < triangles.Count; i++)
        {
            connected[i] = new List<int> { };
            for (int j = 0; j < triangles.Count; j++)
                if (triangles[i].Count(x => triangles[j].Contains(x)) == 2 && lines.All(x => x.Any(y => !triangles[i].Contains(y) || !triangles[j].Contains(y))))
                    connected[i].Add(j);
        }
        Debug.Log("Triangles: " + triangles.Select(x => x.Join(",")).Join());
        Debug.Log("Connected: " + connected.Select(x => x.Join(",")).Join());
        if (connected.Any(x => x.Count > 3))
        {
            Debug.Log("Module shat itself");
            handleBreakdown = true;
            calculating = false;
            return new List<Connection> { };
        }

        //making chains
        bool[] helpers = Enumerable.Repeat(true, 4).Concat(isHelper).ToArray();
        int[] connCount = Enumerable.Range(0, vertices.Count + 4).Select(x => lines.Count(y => y.Contains(x))).ToArray();
        List<List<int>> chains = Enumerable.Range(0, triangles.Count).Select(x => new List<int> { x }).ToList();
        List<List<int>> trueChains = new List<List<int>> { };
        for (int i = 0; i < chains.Count; i++)
        {
            foreach (int path in connected[chains[i].Last()])
            {
                bool continuable = false;
                switch (chains[i].Count)
                {
                    case 1:
                        int start = triangles[chains[i].First()].First(x => !triangles[path].Contains(x));
                        continuable = !helpers[start] && connCount[start] < 3;
                        break;
                    default:
                        continuable = chains[i][chains[i].Count - 2] != path && Enumerable.Range(0, chains[i].Count - 1).All(x => Enumerable.Range(0, 2).Any(y => chains[i][x + y] != new int[] { chains[i].Last(), path }[y]));
                        break;
                }
                if (continuable)
                    chains.Add(chains[i].Concat(new int[] { path }).ToList());
            }
            bool invalid = false;
            if (chains[i].Count >= 2)
            {
                int start = triangles[chains[i][0]].First(x => !triangles[chains[i][1]].Contains(x));
                int end = triangles[chains[i][chains[i].Count - 1]].First(x => !triangles[chains[i][chains[i].Count - 2]].Contains(x));
                invalid |= helpers[start] || helpers[end];
                invalid |= Enumerable.Range(0, connCount.Length).Select(x => new int[] { start, end }.Count(y => y == x) + connCount[x]).Any(x => x > 3);
                bool steady = true;
                //to filter out half of them
                for (int j = 0; j < chains[i].Count && steady; j++)
                    if (chains[i][j] != chains[i][chains[i].Count - 1 - j])
                    {
                        steady = false;
                        invalid |= chains[i][j] > chains[i][chains[i].Count - 1 - j];
                    }
            }
            if (!invalid)
                trueChains.Add(chains[i]);
        }
        Debug.Log("Chain count: " + trueChains.Count);

        //tracing path
        List<Connection> routes = new List<Connection> { };
        foreach (List<int> chain in trueChains)
        {
            Connection route = new Connection(-1, -1, new List<float[]> { });
            float[][][] cuttables = Enumerable.Repeat(new float[][] { }, chain.Count - 1).ToArray();
            if (chain.Count == 1)
            {
                for (int i = 0; i < 3; i++)
                    for (int j = i; j < 3; j++)
                        if (!helpers[triangles[chain.First()][i]] && !helpers[triangles[chain.First()][j]] && Enumerable.Range(0, connCount.Length).Select(x => new int[] { triangles[chain.First()][i], triangles[chain.First()][j] }.Count(y => y == x) + connCount[x]).All(x => x <= 3))
                            if (i == j)
                                route = new Connection(triangles[chain.First()][i], triangles[chain.First()][j], Enumerable.Range(0, 2).Select(x => Enumerable.Range(0, 2).Select(y => triangles[chain.First()].Sum(z => existingVerts[z][y] * (z == triangles[chain.First()][(i + x + 1) % 3] ? 2f : 1f)) / 4f).ToArray()).ToList());
                            else
                                route = new Connection(triangles[chain.First()][i], triangles[chain.First()][j], new List<float[]> { Enumerable.Range(0, 2).Select(x => triangles[chain.First()].Sum(y => existingVerts[y][x]) / 3f).ToArray() });
            }
            else
            {
                bool safe = true;
                Connection line = new Connection(triangles[chain[0]].First(x => !triangles[chain[1]].Contains(x)), triangles[chain[chain.Count - 1]].First(x => !triangles[chain[chain.Count - 2]].Contains(x)), Enumerable.Repeat(new float[0], chain.Count - 1).ToList());
                for (int i = 0; i < chain.Count - 1; i++)
                {
                    float[][] indices = triangles[chain[i]].Where(x => triangles[chain[i + 1]].Contains(x)).Select(x => existingVerts[x]).ToArray();
                    if (Enumerable.Range(0, chain.Count - 1).All(x => chain[i] != chain[x + 1] || chain[i + 1] != chain[x]))
                    {
                        cuttables[i] = indices.ToArray();
                        line.Path[i] = Enumerable.Range(0, 2).Select(x => (indices[0][x] + indices[1][x]) / 2f).ToArray();
                    }
                    else
                        cuttables[i] = indices.Concat(new float[][] { Enumerable.Range(0, 2).Select(x => (indices[0][x] + indices[1][x]) / 2f).ToArray() }).ToArray();
                }
                for (int i = 0; i < chain.Count - 1 && safe; i++)
                {
                    List<float[]> totalPath = new float[][] { existingVerts[line.Start] }.Concat(line.Path).Concat(new float[][] { existingVerts[line.End] }).ToList();
                    List<int> checkIndices = new List<int> { 0, totalPath.Count - 2 };
                    for (int j = 0; j < totalPath.Count - 1; j++)
                        for (int k = 0; k < checkIndices.Count; k++)
                            safe &= (totalPath[j].Length + totalPath[j + 1].Length + totalPath[checkIndices[k]].Length + totalPath[checkIndices[k] + 1].Length < 8) || !Intersect(totalPath[j], totalPath[j + 1], totalPath[checkIndices[k]], totalPath[checkIndices[k] + 1]);
                }
                if (safe)
                {
                    List<Connection> lines2 = new List<Connection> { line };
                    while (lines2.Count > 0 && lines2.First().Path.Any(x => x.Length == 0))
                    {
                        int index = Enumerable.Range(0, chain.Count - 1).First(x => lines2.First().Path[x].Length == 0);
                        int altIndex = Enumerable.Range(0, chain.Count - 1).First(x => chain[index] == chain[x + 1] && chain[index + 1] == chain[x]);
                        float[][] indices = triangles[chain[index]].Where(x => triangles[chain[index + 1]].Contains(x)).Select(x => existingVerts[x]).ToArray();
                        float[][] coords = new float[][] { Enumerable.Range(0, 2).Select(x => (indices[0][x] * 2f + indices[1][x]) / 3f).ToArray(), Enumerable.Range(0, 2).Select(x => (indices[0][x] + indices[1][x] * 2f) / 3f).ToArray() };
                        List<Connection> tempLines = Enumerable.Range(0, lines2.Count * 2).Select(x => new Connection(lines2[x / 2].Start, lines2[x / 2].End, lines2[x / 2].Path)).ToList();
                        lines2 = new List<Connection> { };
                        List<int> checkIndices = new List<int> { index, index + 1, altIndex, altIndex + 1 };
                        for (int i = 0; i < tempLines.Count; i++)
                        {
                            List<float[]> list = tempLines[i].Path.ToList();
                            list[index] = coords[i % 2].ToArray();
                            list[altIndex] = coords[1 - (i % 2)].ToArray();
                            tempLines[i] = new Connection(tempLines[i].Start, tempLines[i].End, list);
                            List<float[]> totalPath = new float[][] { existingVerts[tempLines[i].Start] }.Concat(tempLines[i].Path).Concat(new float[][] { existingVerts[tempLines[i].End] }).ToList();
                            safe = true;
                            for (int j = 0; j < totalPath.Count - 1 && safe; j++)
                                for (int k = 0; k < checkIndices.Count && safe; k++)
                                    safe &= (totalPath[j].Length + totalPath[j + 1].Length + totalPath[checkIndices[k]].Length + totalPath[checkIndices[k] + 1].Length < 8) || !Intersect(totalPath[j], totalPath[j + 1], totalPath[checkIndices[k]], totalPath[checkIndices[k] + 1]);
                            if (safe)
                                lines2.Add(tempLines[i]);
                        }
                    }
                    if (lines2.Count != 0)
                        route = lines2.First();
                }
            }
            if (route.Start != -1)
            {
                //cutting down on path length
                for (int i = 0; i < cuttables.Length; i++)
                    if (cuttables[i].Length == 3)
                        cuttables[i] = Enumerable.Range(0, 3).Where(x => x == 2 || (route.Path[i][0] < cuttables[i][2][0] == cuttables[i][x][0] < cuttables[i][2][0])).Select(x => cuttables[i][x]).ToArray();
                List<float[]> totalPath = new float[][] { existingVerts[route.Start] }.Concat(route.Path).Concat(new float[][] { existingVerts[route.End] }).ToList();
                List<float[]> newPath = new List<float[]> { totalPath.First() };
                int index = 0;
                for (int i = totalPath.Count - 1; i > index; i--)
                {
                    bool safe = true;
                    for (int j = index; j < i - 1; j++)
                        safe &= j >= cuttables.Length || Intersect(totalPath[index], totalPath[i], cuttables[j][0], cuttables[j][1]);
                    if (safe && i - index != totalPath.Count - 1 && lines.All(x => !Intersect(totalPath[index], totalPath[i], existingVerts[x[0]], existingVerts[x[1]])))
                    {
                        index = i;
                        i = totalPath.Count - 1;
                        newPath.Add(totalPath[index].ToArray());
                    }
                }
                if (newPath.Count < 2 || (newPath.Count == 2 && cuttables.Length == 0))
                    newPath = totalPath;
                else
                    newPath.Add(totalPath.Last());
                bool keep = true;
                if (Enumerable.Range(0, newPath.Count - 1).Any(x => lines.Any(y => Intersect(newPath[x], newPath[x + 1], existingVerts[y[0]], existingVerts[y[1]]))))
                {
                    newPath = totalPath;
                    keep = !Enumerable.Range(0, totalPath.Count - 1).Any(x => lines.Any(y => Intersect(totalPath[x], totalPath[x + 1], existingVerts[y[0]], existingVerts[y[1]])));
                }
                route = new Connection(route.Start - 4, route.End - 4, newPath.Skip(1).Take(newPath.Count - 2).ToList());
                if (keep)
                    routes.Add(route);
            }
        }
        Debug.Log("Path count: " + routes.Count);

        //eliminating stupidly complicated paths
        List<Connection> routes2 = new List<Connection> { };
        List<List<int>[]> linkings = new List<List<int>[]> { }; 
        List<int[]> endpoints = new List<int[]> { }; 
        int n = 0;
        List<int> checkVertices = Enumerable.Range(0, helpers.Length).Where(x => !helpers[x]).Concat(new int[] { existingVerts.Count }).ToList();
        Debug.Log(checkVertices.Join());
        foreach (var route in routes)
        {
            n++;
            Debug.Log(n + "/" + routes.Count);
            List<int[]> newLines = lines.ToList();
            newLines.Add(new int[] { route.Start + 4, existingVerts.Count });
            newLines = newLines.Concat(Enumerable.Range(0, route.Path.Count - 1).Select(x => new int[] { existingVerts.Count + x, existingVerts.Count + x + 1 })).ToList();
            newLines.Add(new int[] { route.End + 4, existingVerts.Count + route.Path.Count - 1 });
            List<int[]> newTriangles = Triangulate(existingVerts.Concat(route.Path).ToList(), newLines.ToList());
        
            List<int>[] newConnected = new List<int>[newTriangles.Count];
            for (int i = 0; i < newTriangles.Count; i++)
            {
                newConnected[i] = new List<int> { };
                for (int j = 0; j < newTriangles.Count; j++)
                    if (newTriangles[i].Count(x => newTriangles[j].Contains(x)) == 2 && newLines.All(x => x.Any(y => !newTriangles[i].Contains(y) || !newTriangles[j].Contains(y))))
                        newConnected[i].Add(j);
            }
            if (newConnected.All(x => x.Count <= 3))
            {
                List<int>[] newFullConnected = newConnected.ToArray();
                for (int i = 0; i < newFullConnected.Length; i++)
                    for (int j = 0; j < newFullConnected.Length; j++)
                        for (int k = 0; k < newFullConnected[j].Count; k++)
                            newFullConnected[j] = newFullConnected[j].Concat(newFullConnected[newFullConnected[j][k]]).Distinct().ToList();
                for (int i = 0; i < newFullConnected.Length; i++)
                {
                    newFullConnected[i].Add(i);
                    newFullConnected[i] = newFullConnected[i].Distinct().ToList();
                }
                List<int>[] vertexConnected = Enumerable.Repeat(new List<int> { }, checkVertices.Count).ToArray();
                newTriangles = newTriangles.Select(x => x.Where(y => checkVertices.Contains(y)).ToArray()).ToList();
                for (int i = 0; i < checkVertices.Count; i++)
                    vertexConnected[i] = checkVertices.Where(x => Enumerable.Range(0, newTriangles.Count).Any(y => newTriangles[y].Contains(checkVertices[i]) && newFullConnected[y].Any(z => newTriangles[z].Contains(x)))).ToList();
                routes2.Add(route);
                linkings.Add(vertexConnected);
                endpoints.Add(new int[] { route.Start, route.End }.OrderBy(x => x).ToArray());
            }
        }
        
        List<List<Connection>> routes3 = new List<List<Connection>> { };
        List<List<int>[]> tags = new List<List<int>[]> { };
        List<int[]> tags2 = new List<int[]> { };
        for (int i = 0; i < routes2.Count; i++)
            if (Enumerable.Range(0, tags.Count).Any(x => ListMatch(tags[x], linkings[i]) && Enumerable.Range(0, 2).All(y => endpoints[i][y] == tags2[x][y])))
                routes3[Enumerable.Range(0, tags.Count).First(x => ListMatch(tags[x], linkings[i]) && Enumerable.Range(0, 2).All(y => endpoints[i][y] == tags2[x][y]))].Add(routes2[i]);
            else
            {
                tags.Add(linkings[i]);
                tags2.Add(endpoints[i]);
                routes3.Add(new List<Connection> { routes2[i] });
            }
        routes = routes3.Select(x => x.First(y => y.Path.Count == x.Min(z => z.Path.Count))).OrderByDescending(x => new float[] { existingVerts[x.Start + 4][1], existingVerts[x.End + 4][1] }.Min()).ToList().OrderByDescending(x => new float[] { existingVerts[x.Start + 4][1], existingVerts[x.End + 4][1] }.Max()).ToList();
        Debug.Log("Shortened path count: " + routes.Count);
        calculating = false;
        return routes;
    }

    private bool ListMatch(List<int>[] a, List<int>[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Count != b[i].Count)
                return false;
            for (int j = 0; j < a[i].Count; j++)
                if (a[i][j] != b[i][j])
                    return false;
        }
        return true;
    } 

    private List<int[]> Triangulate(List<float[]> existingVerts, List<int[]> lines)
    {
        List<int[]> triangles = new List<int[]> { };
        for (int i = 0; i < existingVerts.Count; i++)
        {
            List<float[]> tempReorder = existingVerts.Where(x => new int[] { 0, 1 }.Any(y => x[y] != existingVerts[i][y])).OrderBy(x => -((existingVerts[i][1] - x[1]) / (existingVerts[i][0] - x[0]))).ToList();
            tempReorder = tempReorder.Where(x => (existingVerts[i][0] - x[0]) > 0).Concat(tempReorder.Where(x => (existingVerts[i][0] - x[0]) <= 0)).ToList();
            bool[] eliminate = new bool[tempReorder.Count];
            for (int j = 0; j < tempReorder.Count; j++)
                eliminate[j] |= lines.Any(x => Intersect(tempReorder[j], existingVerts[i], existingVerts[x[0]], existingVerts[x[1]])) || tempReorder.Any(x => Mathf.Round(-((existingVerts[i][1] - x[1]) / (existingVerts[i][0] - x[0])) * 10000) == Mathf.Round(-((existingVerts[i][1] - tempReorder[j][1]) / (existingVerts[i][0] - tempReorder[j][0])) * 10000) && Mathf.Abs(existingVerts[i][0] - x[0]) < Mathf.Abs(existingVerts[i][0] - tempReorder[j][0]));
            tempReorder = Enumerable.Range(0, tempReorder.Count).Where(x => !eliminate[x]).Select(x => tempReorder[x]).ToList();
            for (int j = 0; j < tempReorder.Count; j++)
            {
                int[] triangulVerts = new int[] { i, Enumerable.Range(0, existingVerts.Count).First(x => new int[] { 0, 1 }.All(y => existingVerts[x][y] == tempReorder[j][y])), Enumerable.Range(0, existingVerts.Count).First(x => new int[] { 0, 1 }.All(y => existingVerts[x][y] == tempReorder[(j + 1) % tempReorder.Count][y])) }.OrderBy(x => x).ToArray();
                if (triangles.All(x => Enumerable.Range(0, 3).Any(y => triangulVerts[y] != x[y])) && Enumerable.Range(0, 3).All(y => lines.All(x => !Intersect(existingVerts[triangulVerts[y]], existingVerts[triangulVerts[(y + 1) % 3]], existingVerts[x[0]], existingVerts[x[1]]))) && Enumerable.Range(0, existingVerts.Count).Where(x => !triangulVerts.Contains(x)).All(x => Enumerable.Range(0, 3).Count(y => Intersect(existingVerts[x], new float[] { 1, 1 }, existingVerts[triangulVerts[y]], existingVerts[triangulVerts[(y + 1) % 3]])) != 1))
                {
                    triangles.Add(triangulVerts);
                    for (int k = 0; k < 3; k++)
                        if (lines.All(x => new int[] { 0, 1 }.Any(y => !x.Contains(triangulVerts[(k + y) % 3]))))
                            lines.Add(Enumerable.Range(0, 3).Where(x => new int[] { 0, 1 }.Select(y => (k + y) % 3).Contains(x)).Select(x => triangulVerts[x]).ToArray());
                }
            }
        }
        return triangles;
    }

    //a^2+b^2=c^2
    private float Pytha(float x, float y)
    {
        return Mathf.Sqrt(x * x + y * y);
    }

    //if ab/cd, true
    private bool Intersect(float[] a, float[] b, float[] c, float[] d)
    {
        if (new float[][] { a, b }.Any(x => new float[][] { c, d }.Any(y => new int[] { 0, 1 }.All(z => x[z] == y[z]))))
            return false;
        float dA = (c[1] - d[1]) / (c[0] - d[0]) - (a[1] - b[1]) / (a[0] - b[0]);
        float dB = (a[1] - (a[1] - b[1]) / (a[0] - b[0]) * a[0]) - (c[1] - (c[1] - d[1]) / (c[0] - d[0]) * c[0]);
        float X = dB / dA;
        return (new float[][][] { new float[][] { a, b }, new float[][] { c, d } }.All(x => X > x.Select(y => y[0]).Min() && X < x.Select(y => y[0]).Max()));
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "'!{0} next 69' to press the next button 69 times. '!{0} enter' to enter the currently shown configuration.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        yield return null;
        if (calculating)
        {
            yield return "sendtochaterror Module cannot be interacted with during calculation.";
        }
        else
        {
            command = command.ToLowerInvariant();
            if (command == "enter")
                Buttons[0].OnInteract();
            else if (Regex.IsMatch(command, @"^next\s\d+$"))
            {
                yield return "solve";
                int a;
                if (int.TryParse(command.Split(' ')[1], out a))
                    for (int i = 0; i < a % validpaths.Count; i++)
                    {
                        Buttons[1].OnInteract();
                        yield return null;
                    }
                else
                    yield return "sendtochaterror Invalid command.";
            }
            else
                yield return "sendtochaterror Invalid command.";
            yield return null;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return true;
        while (!solved)
        {
            while (calculating && !solved)
                yield return true;
            yield return null;
            if (validpaths.Count != 0)
            {
                index = Rnd.Range(0, validpaths.Count);
                Buttons[0].OnInteract();
            }
        }
    }
}