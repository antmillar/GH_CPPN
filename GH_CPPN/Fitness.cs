﻿using GH_CPPN;
using MathNet.Numerics.LinearAlgebra;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using TopolEvo.NEAT;
using ImageInfo;
using TopolEvo.Utilities;

namespace TopolEvo.Fitness
{

    /// <summary>
    /// Bit Field to represent which metrics to use in the Fitness Function
    /// </summary>
    [Flags]
    public enum Metrics
    {
        Height = 1,
        Depth = 2,
        TopLayer = 4,
        BottomLayer = 8,
        Displacement = 16,
        L1 = 32,
        L2 = 64,
        Size = 128,
        Luminance = 256,
        Contrast = 512,
        Pattern = 1024
    }

    public static class Fitness
    {

        /// <summary> 
        /// Static class where user create a fitness function, must take input genomes and assign the fitness attribute of each genome
        /// </summary>
        /// 
           

        public static List<string> Function(Population pop, Dictionary<int, Matrix<double>> outputs, Matrix<double> coords, Matrix<double> outputTargets, int subdivisions, Metrics metrics)
        {
            //can manually specify a target occupancy using this function
            //occupancyTarget = CreateTargetOccupancy(outputs[pop.Genomes[0].ID].RowCount, outputs[pop.Genomes[0].ID].ColumnCount, coords);

            //convert to binary
            //var occupancy = Fitness.OccupancyFromOutputs(outputs);
            var occupancy = outputs;

            var fitnesses = new List<double>();
            var fitnessStrings = new List<string>();
            Accord.Math.Random.Generator.Seed = 0;

            if ((metrics & Metrics.Pattern) == Metrics.Pattern)
            {
                var featDistance = ImageAnalysis.GetFeatureDistances(occupancy, outputTargets, subdivisions);


               // totalFitness += featDistance;
               // fitnessString += $" | Pattern : {Math.Round(featDistance, 2)}";
            }

            //loop over each member of the population and calculate fitness components
            Parallel.ForEach(occupancy.Keys, (key) =>
            //foreach (KeyValuePair<int, Matrix<double> > output in occupancy)
            {
                //All Weights are Max 10, Min 0

                var outputID = key;
                var outputValues = occupancy[key];

                var totalFitness = 0.0;
                var fitnessString = "";

                //flatten occupancy matrix
                var vals = outputValues.ToRowMajorArray();

                var featDistances = outputs.Select(x => ImageAnalysis.GetFeatureDistance(x.Value, outputTargets, subdivisions)).ToList();

                if ((metrics & Metrics.Pattern) == Metrics.Pattern)
                {
                    var featDistance = ImageAnalysis.GetFeatureDistance(outputValues, outputTargets, subdivisions);


                    totalFitness += featDistance;
                    fitnessString += $" | Pattern : {Math.Round(featDistance, 2)}";
                }

                if ((metrics & Metrics.Luminance) == Metrics.Luminance)
                {
                    var outputLum = ImageAnalysis.GetMeanLuminance(outputValues, subdivisions);
                    var targetLum = ImageAnalysis.GetMeanLuminance(outputTargets, subdivisions);

                    var absdiff =  Math.Abs(outputLum - targetLum);

                    absdiff = Utils.Map(absdiff, 0, 1.0, 0, 10);

                    totalFitness += absdiff;
                    fitnessString += $" | Luminance : {Math.Round(absdiff, 2)}";
                }


                if ((metrics & Metrics.Contrast) == Metrics.Contrast)
                {
                    var outputContrast = ImageAnalysis.GetContrast(outputValues, subdivisions);
                    var targetContrast = ImageAnalysis.GetContrast(outputTargets, subdivisions);

                    var absdiff = Math.Abs(outputContrast - targetContrast);

                    absdiff = Utils.Map(absdiff, 0, 1.0, 0, 10);

                    totalFitness += absdiff;
                    fitnessString += $" | Contrast : {Math.Round(absdiff, 2)}";
                }

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

                    fitnessHeight = Utils.Map(fitnessHeight, 0, subdivisions, 0, 10);

                    totalFitness += fitnessHeight;
                    fitnessString += $" | Height : {Math.Round(fitnessHeight, 2)}";
                }


                if ((metrics & Metrics.Depth) == Metrics.Depth)
                {
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

                    fitnessDepth = Utils.Map(fitnessDepth, 0, subdivisions, 0, 10);

                    totalFitness += fitnessDepth;
                    fitnessString += $" | Depth : {Math.Round(fitnessDepth, 2)}";
                }

                if ((metrics & Metrics.Size) == Metrics.Size)
                {
                    //Cell Count
                    var occupancyCount = vals.Sum();
                    var fitnessSize = (10.0 - (10.0 * occupancyCount / (Math.Pow(subdivisions, coords.ColumnCount)))); //punish high cell count

                    totalFitness += fitnessSize;
                    fitnessString += $" | Size : {Math.Round(fitnessSize, 2)}";
                }

                if ((metrics & Metrics.Displacement) == Metrics.Displacement)
                {
                    //FEM
                    var fitnessDisplacement = 0.0;
                    try
                    {
                        var FEMModel = FEM.CreateModel(coords, outputValues, subdivisions, subdivisions, subdivisions);
                        pop.GetGenomeByID(outputID).FEMModel = FEMModel;
                        var displacements = FEM.GetDisplacements(FEMModel);
                        var scaled = displacements.Max(i => Math.Abs(i)) * 10e7;
                        var standardised = 10 - Utils.Map(scaled, 0.01, 1.00, 0, 10);

                        if (standardised > 10) standardised = 10.0;
                        if (standardised < 0) standardised = 0.0;

                        fitnessDisplacement = standardised ;
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
                if ((metrics & Metrics.L1) == Metrics.L1 & outputTargets != null)
                {
                    var fitnessL1Norm = (outputValues - outputTargets).PointwiseAbs().ToRowMajorArray().Sum();
                    fitnessL1Norm = Utils.Map(fitnessL1Norm, 0.00, Math.Sqrt(subdivisions * subdivisions), 0, 10.0);
                    totalFitness += fitnessL1Norm;
                    fitnessString += $" | L1Norm : {Math.Round(fitnessL1Norm, 2)}";

                }


                if ((metrics & Metrics.L2) == Metrics.L2 & outputTargets != null)
                {
                    var fitnessL2Norm = Math.Sqrt((outputValues - outputTargets).PointwisePower(2).ToRowMajorArray().Sum());
                    fitnessL2Norm = Utils.Map(fitnessL2Norm, 0.00, Math.Sqrt(subdivisions * subdivisions), 0, 10.0);
                    totalFitness += fitnessL2Norm;
                    fitnessString += $" | L2Norm : {Math.Round(fitnessL2Norm, 2)}";

                }

           

                fitnessString += $" | Total : {Math.Round(totalFitness, 2)}";
                pop.GetGenomeByID(outputID).Fitness = totalFitness;
                pop.GetGenomeByID(outputID).FitnessAsString = fitnessString;
            }
            );


            SortByFitness(pop);

            fitnesses = pop.Genomes.Select(i => i.Fitness).ToList();

            fitnessStrings = pop.Genomes.Select(i => i.FitnessAsString).ToList();

            return fitnessStrings;
            //have a config setting for min max fitnesses
        }

        internal static void SortByFitness(Population pop)
        {
            if (Config.fitnessTarget == "min")
            {
                pop.Genomes = pop.Genomes.OrderBy(x => x.Fitness).ToList();
            }
            else if (Config.fitnessTarget == "max")
            {
                pop.Genomes = pop.Genomes.OrderByDescending(x => x.Fitness).ToList();
            }

            //totalFitness = Genomes.Select(x => 1 / x.Fitness).Sum();
        }

        //greyscale from image
        internal static Matrix<double> PixelsFromImage(int subdivisions, Matrix<double> coords, Bitmap bmTarget)
        {
            var pixelTarget = Matrix<double>.Build.Dense(subdivisions * subdivisions, 1, 0.0);

            var width = bmTarget.Width;
            var height = bmTarget.Height;

            var xStep = width / subdivisions;
            var yStep = height / subdivisions;
            var counter = 0;

            for (int y = 0; y < subdivisions; y++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    pixelTarget[counter, 0] = bmTarget.GetPixel(x * xStep, y * yStep).R / 255.0;
                    counter++;
                }
            }

            return pixelTarget;
        }


        /// <summary>
        /// Find points in voxel grid contained inside the target mesh
        /// </summary>
        public static Matrix<double> VoxelsFromMesh(int rows, int cols, Matrix<double> coords, Mesh meshTarget)
        {

            meshTarget.FillHoles();

            var voxelsTarget= Matrix<double>.Build.Dense(rows, cols, 0.0);

            for (int i = 0; i < voxelsTarget.RowCount; i++)
            {
                ////equation of circle
                var pt = new Point3d(coords[i, 0], coords[i, 1], coords[i, 2]);

                if (meshTarget.IsPointInside(pt, 0.5, false))
                {
                    voxelsTarget[i, 0] = 1.0;
                }

            }

            return voxelsTarget;
        }

        /// <summary>
        /// Converts an output with continuous values to a discrete occupancy
        /// </summary>
        /// <param name="outputs">The outputs from the population of NEAT Genomes</param>
        /// 
        public static Dictionary<int, Matrix<double>> OccupancyFromOutputs(Dictionary<int, Matrix<double>> outputs)
        {
            var occupancy = new Dictionary<int, Matrix<double>>();

            //overwrite continuous values between 0-1 to either 0 or 1
            foreach (int key in outputs.Keys.ToList())
            {
                occupancy[key] = outputs[key].Map(i => i > 0.5 ? 1.0 : 0.0);
            }

            return occupancy;
        }



        //manually specify a target occupancy grid
        public static Matrix<double> CreateTargetOccupancy(int rows, int cols, Matrix<double> coords)
        {

            var targets = Matrix<double>.Build.Dense(rows, cols, 0.0);

            for (int i = 0; i < targets.RowCount; i++)
            {
                ////equation of circle
                if (Math.Sqrt(Math.Pow(coords[i, 0], 2) + Math.Pow(coords[i, 1], 2)) <= 0.3)
                {
                    targets[i, 0] = 1.0;
                }

                //vert bar
                //if (coords[i, 0] < -0.25 || coords[i, 0] > 0.25)
                //{
                //    targets[i, 0] = 1.0;
                //}


                //vert partition
                //if (coords[i, 0] < 0.0)
                //{
                //    targets[i, 0] = 1.0;
                //}

                //all white

                //targets[i, 0] = 1.0;

            }

            return targets;
        }

    }
}
