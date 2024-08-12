using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace LevelGeneration
{
    /// <summary>
    /// Generates level using the wave-function-collapse algorithm.
    /// </summary>
    public class LevelGenerator : GridGenerator
    {
        public MainUI mainUI;
        /// <summary>
        /// The modules.
        /// </summary>
        public List<Module> modules;

        /// <summary>
        /// The start module.
        /// </summary>
        public Module startModule;

        /// <summary>
        /// The goal module.
        /// </summary>
        public Module goalModule;

        /// <summary>
        /// Stores the cells in a heap having the closest cell to being solved as first element.
        /// </summary>
        public Heap<Cell> orderedCells;

        /// <summary>
        /// RNG seed.
        ///
        /// If set to -1 a random seed will be selected for every level generation.
        /// </summary>
        [Tooltip("If set to -1 a random seed will be selected for every level generation.")]
        public int seed;

        private bool isGenerating = false;

        bool failed = false;

        private void Start()
        {
            // Start the level generation process
            if (mainUI.totalAttempts < attempts)
            {
                mainUI.cameraController.AdjustCamera(width, height);
                GenerateLevel();
            }
        }

        private void Update()
        {
            // Check if generation is not running and there are remaining attempts
            if (!isGenerating && mainUI.totalAttempts < attempts)
            {
                GenerateLevel();
            }
        }

        /// <summary>
        /// Wave-function-collapse algorithm.
        /// </summary>
        public void GenerateLevel()
        {
            isGenerating = true; // Mark generation as running

            RemoveGrid();
            GenerateGrid(this);

            var finalSeed = seed != -1 ? seed : Environment.TickCount;

            Random.InitState(finalSeed);

            // instantiate cells heap
            orderedCells = new Heap<Cell>(cells.GetLength(0) * cells.GetLength(1));

            for (var i = 0; i < cells.GetLength(0); i++)
            for (var j = 0; j < cells.GetLength(1); j++)
            {
                orderedCells.Add(cells[i, j]);
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // make sure the level fits the initial constraints
            ApplyInitialConstraints();

            // wave-function-collapse algorithm
            while (orderedCells.Count > 0)
            {
                // get cell that is closest to being solved (can also already be solved)
                orderedCells = SortOrderedCells(orderedCells);
                var cell = orderedCells.GetFirst();


                if (cell.possibleModules.Count == 1)
                {
                    // cell is already solved -> remove finished cell from heap
                    cell.isFinal = true;
                    orderedCells.RemoveFirst();
                    AdaptNeighboursFromCellToPossibleFromTileSet(cell);
                }
                else
                {
                    // set a random module for this cell
                    try
                    {
                        cell.SetModule(cell.possibleModules[Random.Range(0, cell.possibleModules.Count)]);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        failed = true;
                        mainUI.RunFailed();
                        break;
                    }
                }
            }

            stopwatch.Stop();
            // If failed, do not print time elapsed and do not instantiate the cells, because the correct solution will be instantiated in n+1 failed attempts
            if (!failed)
            {
                // Instantiate module game objects
                foreach (var cell in cells)
                {
                    var t = cell.transform;
                    Instantiate(cell.possibleModules[0].moduleGO, t.position, Quaternion.identity, t);
                }
                mainUI.RunSuceeded();
            }else{
                foreach (var cell in cells)
                {
                    var t = cell.transform;
                    if(cell.possibleModules.Count == 0){
                        cell.possibleModules.Add(startModule);
                    }
                    Instantiate(cell.possibleModules[0].moduleGO, t.position, Quaternion.identity, t);
                }
            }

            // If there are remaining attempts, schedule the next generation run
            if (mainUI.totalAttempts >= attempts)
            {
                mainUI.cameraController.AdjustCamera(width, height);
                Debug.Log($"Wave-function-collapse algorithm finished in {stopwatch.Elapsed.TotalMilliseconds}ms (Seed: {finalSeed})");
            }

            isGenerating = false; // Mark generation as not running
        }

        private void AdaptNeighboursFromCellToPossibleFromTileSet(Cell cell)
        {
            // Iterate through each neighbour of the cell
            for (int i = 0; i < cell.neighbours.Length; i++)
            {
                var neighbour = cell.neighbours[i];

                // If the neighbour is null, already final, or has only one possible module, skip to the next one
                if (neighbour == null || neighbour.isFinal || neighbour.possibleModules.Count <= 1)
                {
                    continue;
                }

                // Determine the correct prefab list based on the neighbour's position
                List<GameObject> relevantPrefabs = null;
                switch (i)
                {
                    case 0: // Up
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsUp).ToList();
                        ApplyFilterToNeighbour(neighbour, new int[] { 1, 3 });
                        break;
                    case 1: // Left
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsLeft).ToList();
                        ApplyFilterToNeighbour(neighbour, new int[] { 0, 2 });
                        break;
                    case 2: // Bottom
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsBottom).ToList();
                        ApplyFilterToNeighbour(neighbour, new int[] { 1, 3 });
                        break;
                    case 3: // Right
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsRight).ToList();
                        ApplyFilterToNeighbour(neighbour, new int[] { 0, 2 });
                        break;
                }

                // Create a list to store the modules to be removed
                List<Module> modulesToRemove = new List<Module>();

                // Iterate through each possible module of the neighbour
                foreach (var neighbourModule in neighbour.possibleModules)
                {
                    // Check if this neighbour's module's GameObject is not in the relevant prefabs list
                    bool matchFound = relevantPrefabs.Any(prefab => prefab == neighbourModule.moduleGO);

                    // If no match was found, add this module to the removal list
                    if (!matchFound)
                    {
                        modulesToRemove.Add(neighbourModule);
                    }
                }

                // Remove the non-matching modules from the neighbour's possibleModules list
                foreach (var module in modulesToRemove)
                {
                    neighbour.possibleModules.Remove(module);
                }
            }
        }

        private void ApplyFilterToNeighbour(Cell cell, int[] checkIndices)
        {
            // Iterate through the specified neighbours of the cell
            for (int i = 0; i < checkIndices.Length; i++)
            {
                var neighbour = cell.neighbours[checkIndices[i]];

                // If the neighbour is null, already final, or has only one possible module, skip to the next one
                if (neighbour == null || neighbour.isFinal || neighbour.possibleModules.Count <= 1)
                {
                    continue;
                }

                // Determine the correct prefab list based on the neighbour's position
                List<GameObject> relevantPrefabs = null;
                switch (checkIndices[i])
                {
                    case 0: // Up
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsUp).ToList();
                        break;
                    case 1: // Left
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsLeft).ToList();
                        break;
                    case 2: // Bottom
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsBottom).ToList();
                        break;
                    case 3: // Right
                        relevantPrefabs = cell.possibleModules.SelectMany(module => module.prefabsRight).ToList();
                        break;
                }

                // Create a list to store the modules to be removed
                List<Module> modulesToRemove = new List<Module>();

                // Iterate through each possible module of the neighbour
                foreach (var neighbourModule in neighbour.possibleModules)
                {
                    // Check if this neighbour's module's GameObject is not in the relevant prefabs list
                    bool matchFound = relevantPrefabs.Any(prefab => prefab == neighbourModule.moduleGO);

                    // If no match was found, add this module to the removal list
                    if (!matchFound)
                    {
                        modulesToRemove.Add(neighbourModule);
                    }
                }

                // Remove the non-matching modules from the neighbour's possibleModules list
                foreach (var module in modulesToRemove)
                {
                    neighbour.possibleModules.Remove(module);
                }
            }
        }

        /// <summary>
        /// Resolve all initial constraints.
        /// </summary>
        private void ApplyInitialConstraints()
        {
            //StartGoalConstraint();

            //Include this if you want streets to avoid going outside the zone

            //BorderOutsideConstraint();
        }

        /// <summary>
        /// Initial constraint: There can only be border on the outside.
        /// </summary>
        private void BorderOutsideConstraint()
        {
            var bottomFilter = new EdgeFilter(2, Module.EdgeConnectionTypes.Block, true);
            var topFilter = new EdgeFilter(0, Module.EdgeConnectionTypes.Block, true);
            var leftFilter = new EdgeFilter(1, Module.EdgeConnectionTypes.Block, true);
            var rightFilter = new EdgeFilter(3, Module.EdgeConnectionTypes.Block, true);

            // filter bottom and top cells for only border
            for (var i = 0; i < 2; i++)
            {
                var z = i * (height - 1);

                for (var x = 0; x < width; x++)
                {
                    cells[x, z].FilterCell(i == 0 ? bottomFilter : topFilter);
                }
            }

            // filter left and right cells for only border
            for (var i = 0; i < 2; i++)
            {
                var x = i * (width - 1);

                for (var z = 0; z < height; z++)
                {
                    cells[x, z].FilterCell(i == 0 ? leftFilter : rightFilter);
                }
            }
        }

        /// <summary>
        /// Initial constraint: Place one start and one goal module.
        /// </summary>
        private void StartGoalConstraint()
        {
            var startCell = cells[Random.Range(0, cells.GetLength(0)), Random.Range(0, cells.GetLength(1) - 1)];
            Cell goalCell;

            startCell.SetModule(startModule);

            do
            {
                goalCell = cells[Random.Range(0, cells.GetLength(0)), Random.Range(1, cells.GetLength(1))];
            } while (goalCell == startCell);

            goalCell.SetModule(goalModule);
        }

        public Heap<Cell> SortOrderedCells(Heap<Cell> heap)
        {
            List<Cell> sortedList = new List<Cell>();

            // Extract all elements from the heap
            while (heap.Count > 0)
            {
                sortedList.Add(heap.GetFirst());
                heap.RemoveFirst();
            }

            // Sort the list based on possibleModules.Count
            sortedList.Sort((cell1, cell2) => cell1.possibleModules.Count.CompareTo(cell2.possibleModules.Count));

            // Create a new heap with the sorted elements
            Heap<Cell> sortedHeap = new Heap<Cell>(sortedList.Count);

            // Add sorted elements back to the new heap
            foreach (var cell in sortedList)
            {
                sortedHeap.Add(cell);
            }

            return sortedHeap; // Return the sorted heap
        }
    }
}