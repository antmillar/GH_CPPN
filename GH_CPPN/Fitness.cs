﻿using GH_CPPN;
using MathNet.Numerics.LinearAlgebra;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TopolEvo.NEAT;

namespace TopolEvo.Fitness
{

    /// <summary>
    /// Bit Field to represent which metrics to use in the Fitness Function
    /// </summary>
    [Flags]
    public enum Metrics
    {
        Size = 0,
        Height = 1,
        Depth = 2,
        TopLayer = 4,
        BottomLayer = 8,
        Displacement = 16,
        L1 = 32,
        L2 = 64
    }

    public static class Fitness
    {

        public static double Map(double num, double x1, double y1, double x2, double y2)
        {
            var result = ((num - x1) / (y1 - x1)) * (y2 - x2) + x2;  

            return result;
        }




        /// <summary> 
        /// Static class where user create a fitness function, must take input genomes and assign the fitness attribute of each genome
        /// </summary>
        /// 

        public static List<string> Function(Population pop, Dictionary<int, Matrix<double>> outputs, Matrix<double> coords, Matrix<double> occupancyTarget, int subdivisions, Metrics metrics)
        {
            //can manually specify a target occupancy using this function
            //var occupancyTarget = CreateTargetOccupancy(outputs[pop.Genomes[0].ID].RowCount, outputs[pop.Genomes[0].ID].ColumnCount, coords);

            var fitnesses = new List<double>();
            var fitnessStrings = new List<string>();
                
            //loop over each member of the population and calculate fitness components
            foreach (KeyValuePair<int, Matrix<double> > output in outputs)
            {
                //All Weights are Max 10, Min 0

                var outputID = output.Key;
                var occupancyOutput = output.Value;

                var totalFitness = 0.0;
                var fitnessString = "";

                //flatten occupancy matrix
                var vals = occupancyOutput.ToRowMajorArray();

                if ((metrics & Metrics.Height) == Metrics.Height)
                {
                    //Height
                    var fitnessHeight = 0.0;
                    for (int i = 0; i < subdivisions; i++)
                    {
                        var cellsInHoriz = vals.Skip(i * subdivisions * subdivisions).Take(subdivisions * subdivisions).Sum();

                        if (cellsInHoriz > 0)
                        {
                            fitnessHeight++;
                        }
                    }

                    fitnessHeight = Map(fitnessHeight, 0, subdivisions, 0, 10);

                    totalFitness += fitnessHeight;
                    fitnessString += $" | Height : {Math.Round(fitnessHeight, 2)}"; 
                }


                if ((metrics & Metrics.Depth) == Metrics.Depth)
                {
                    //Depth
                    var fitnessDepth = 0.0;

                    for (int i = 0; i < subdivisions; i++)
                    {
                        var cellsInVert = 0.0;

                        for (int j = 0; j < subdivisions; j++)
                        {
                            cellsInVert += vals.Skip(j * subdivisions * subdivisions + i * subdivisions).Take(subdivisions).Sum();
                        }

                        if (cellsInVert > 0)
                        {
                            fitnessDepth++;
                        }
                    }

                    fitnessDepth = Map(fitnessDepth, 0, subdivisions, 0, 10);

                    totalFitness += fitnessDepth;
                    fitnessString += $" | Depth : {Math.Round(fitnessDepth, 2)}";
                }

                if ((metrics & Metrics.Size) == Metrics.Size)
                {
                    //Cell Count
                    var occupancyCount = vals.Sum();
                    var fitnessSize = (10.0 - (10.0 * occupancyCount / (subdivisions * subdivisions * subdivisions))); //punish high cell count

                    totalFitness += fitnessSize;
                    fitnessString += $" | Size : {Math.Round(fitnessSize, 2)}";
                }

                if((metrics & Metrics.Displacement) == Metrics.Displacement)
                {
                    //FEM
                    var fitnessDisplacement = 0.0;
                    try
                    {
                        var FEMModel = FEM.CreateModel(coords, occupancyOutput, subdivisions, subdivisions, subdivisions);
                        pop.GetGenomeByID(outputID).FEMModel = FEMModel;
                        var displacements = FEM.GetDisplacements(FEMModel);
                        var scaled = displacements.Max(i => Math.Abs(i)) * 10e7;
                        var map = 10 - Map(scaled, 0.01, 1.00, 0, 10);

                        if (map > 10) map = 10.0;
                        if (map < 0) map = 0.0;


                        fitnessDisplacement = map / 2.0;
                    }
                    catch
                    {
                        fitnessDisplacement = -10.0;
                    }

                    totalFitness += fitnessDisplacement;
                    fitnessString += $" | Displacement : {Math.Round(fitnessDisplacement, 2)}";
                }


                if ((metrics & Metrics.BottomLayer) == Metrics.BottomLayer)
                {
                    //Bottom  Layer
                    var bottomLayerCount = vals.Take(subdivisions * subdivisions).Sum();

                    var fitnessBottomLayer = 0.0;

                    if (bottomLayerCount > 50.0)
                    { fitnessBottomLayer = 10.0; }
                    else
                    {
                        fitnessBottomLayer = bottomLayerCount / 5.0;
                    }

                    totalFitness += fitnessBottomLayer;
                    fitnessString += $" | BottomLayer : {Math.Round(fitnessBottomLayer, 2)}";

                }

                if ((metrics & Metrics.TopLayer) == Metrics.TopLayer)
                {
                    //Top Layer
                    var topLayerCount = vals.Skip(subdivisions * subdivisions * (subdivisions - 1)).Take(subdivisions * subdivisions).Sum();

                    var fitnessTopLayer = 0.0;

                    if (topLayerCount > 60.0)
                    { fitnessTopLayer = 10.0; }
                    else
                    {
                        fitnessTopLayer = topLayerCount / 6.0;
                    }

                    totalFitness += fitnessTopLayer;
                    fitnessString += $" | TopLayer : {Math.Round(fitnessTopLayer, 2)}";

                }

                //could tidy this error checking
                if ((metrics & Metrics.L1) == Metrics.L1 & occupancyTarget != null)
                {
                    var fitnessL1Norm = (occupancyOutput - occupancyTarget).PointwiseAbs().ToRowMajorArray().Sum();
                    totalFitness += fitnessL1Norm;
                    fitnessString += $" | L1Norm : {Math.Round(fitnessL1Norm, 2)}";

                }


                if ((metrics & Metrics.L2) == Metrics.L2 & occupancyTarget != null)
                {
                    var fitnessL2Norm = Math.Sqrt((occupancyOutput - occupancyTarget).PointwisePower(2).ToRowMajorArray().Sum());
                    totalFitness += fitnessL2Norm;
                    fitnessString += $" | L2Norm : {Math.Round(fitnessL2Norm, 2)}";

                }

                fitnessString += $" | Total : {Math.Round(totalFitness, 2)}";
                pop.GetGenomeByID(outputID).Fitness = totalFitness;
                pop.GetGenomeByID(outputID).FitnessAsString = fitnessString;
            }

            pop.SortByFitness();

            fitnesses = pop.Genomes.Select(i => i.Fitness).ToList();

            fitnessStrings = pop.Genomes.Select(i => i.FitnessAsString).ToList();

            return fitnessStrings;
            //have a config setting for min max fitnesses
        }


        /// <summary>
        /// Find points in voxel grid contained inside the target mesh
        /// </summary>
        public static Matrix<double> OccupancyFromMesh(int rows, int cols, Matrix<double> coords, Mesh meshTarget)
        {

            meshTarget.FillHoles();

            var occupancyTarget= Matrix<double>.Build.Dense(rows, cols, 0.0);

            for (int i = 0; i < occupancyTarget.RowCount; i++)
            {
                ////equation of circle
                var pt = new Point3d(coords[i, 0], coords[i, 1], coords[i, 2]);

                if (meshTarget.IsPointInside(pt, 0.5, false))
                {
                    occupancyTarget[i, 0] = 1.0;
                }

            }

            return occupancyTarget;
        }

        /// <summary>
        /// Converts an output with continuous values to a discrete occupancy
        /// </summary>
        /// <param name="outputs">The outputs from the population of NEAT Genomes</param>
        /// 
        public static Dictionary<int, Matrix<double>> OccupancyFromOutputs(Dictionary<int, Matrix<double>> outputs)
        {
            //overwrite continuous values between 0-1 to either 0 or 1
            foreach (int key in outputs.Keys.ToList())
            {
                outputs[key] = outputs[key].Map(i => i > 0.5 ? 1.0 : 0.0);
            }

            return outputs;
        }



        //manually specify a target occupancy grid
        public static Matrix<double> CreateTargetOccupancy(int rows, int cols, Matrix<double> coords)
        {

            var targets = Matrix<double>.Build.Dense(rows, cols, 0.0);

            for (int i = 0; i < targets.RowCount; i++)
            {
                ////equation of circle
                if (Math.Pow(coords[i, 0], 2) + Math.Pow(coords[i, 1], 2) + Math.Pow(coords[i, 2], 2) < 0.2)
                {
                    //values[i] = 1.0;
                    targets[i, 0] = 1.0;
                }

                //vert bar
                //if (coords[i, 0] < -0.25 || coords[i, 0] > 0.25)
                //{
                //    values[i] = 1.0;
                //}


                //vert partition
                //if (coords[i, 0] < 0.0)
                //{
                //    values[i] = 1.0;
                //}

                //all white

                //values[i] = 1.0;

            }

            return targets;
        }

    }
}
