﻿using Grasshopper.Kernel;
using NumSharp;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TopolEvo.Architecture;
using TopolEvo.Display;
using TopolEvo.Fitness;
//custom libraries
using TopolEvo.NEAT;

namespace GH_CPPN
{
    public class GHComponent : GH_Component
    {
        public GHComponent() : base("Topology Evolver", "TopolEvo", "Evolving CPPNs", "TopologyEvolver", "2D")
        {

        }

        //fields
        private bool init;
        private List<Mesh> meshes = new List<Mesh>();
        private Population pop;
        private List<double> fits;
        private NDArray coords;
        private Dictionary<int, NDArray> outputs;


        public override Guid ComponentGuid
        {
            // Don't copy this GUID, make a new one
            get { return new Guid("ab047315-77a4-45ef-8039-44cd6428b913"); }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            //pManager.AddTextParameter("input text", "i", "string to reverse", GH_ParamAccess.item);
            pManager.AddBooleanParameter("toggle generation", "toggle", "run the next generation", GH_ParamAccess.item);
            pManager.AddNumberParameter("survival cutoff", "survival cutoff", "survival cutoff", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            //pManager.AddPointParameter("points", "pts", "grid points", GH_ParamAccess.list);
            pManager.AddMeshParameter("mesh grid", "mg", "grid of meshes", GH_ParamAccess.list);
            pManager.AddTextParameter("fitnesses", "fitnesses", "output of fitnesses", GH_ParamAccess.list);
            pManager.AddNumberParameter("mean fitness", "mean fitness", "means of fitnesses", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            bool button = false;
            double cutoff = 0.5;

            if (!DA.GetData(0, ref button)) { return; }
            if (!DA.GetData(1, ref cutoff)) { return; }

            if (button == null) { return; }
            if (cutoff == null) { return; }


            //var linear = new temp.CustomModel();
            //NDArray activations = linear.ForwardPass(coords);

            //var mlp = new temp.MLP(2, 1, 8);
            //NDArray activations = mlp.ForwardPass(coords);
            int width = 20;
            int popSize = 50;

            if (!init)
            {
                init = true;

                //initialise globals

                pop = new Population(popSize);

                coords = np.ones((width * width, 2));

                PopulateCoords(width);

                outputs = new Dictionary<int, NDArray>();

                outputs = pop.Evaluate(coords);
                fits = Fitness.Function(pop, outputs, coords);
                pop.SortByFitness();

   
                meshes = GenerateMeshes(pop, outputs, width, popSize);
            }

            //paint mesh using outputs

            if (button)
            {
                Config.survivalCutoff = cutoff;
                Run(50, width, popSize);
            }

            //output data from GH component
            DA.SetDataList(0, meshes);
            DA.SetDataList(1, fits);
            DA.SetData(2, fits.Average());
        }


        private List<Mesh> Run(int generations, int width, int popSize)
        {
            for (int i = 0; i < generations; i++)
            {

                outputs.Clear();
                meshes.Clear();

                pop.NextGeneration();

                outputs = pop.Evaluate(coords);
                fits = Fitness.Function(pop, outputs, coords);

                pop.SortByFitness();

            }

            meshes = GenerateMeshes(pop, outputs, width, popSize);

            return meshes;
        }
        
        private List<Mesh> GenerateMeshes(Population pop, Dictionary<int, NDArray> outputs, int width, int popSize)
        {
            for (int i = 0; i < pop.Genomes.Count; i++)
            {
                var drawing = new Drawing(width, -popSize / 2 * width + i * width, 0);
                Mesh combinedMesh = drawing.Paint(outputs[pop.Genomes[i].ID]);
                meshes.Add(combinedMesh);

            }
            return meshes;
        }

        private void PopulateCoords(int width)
        {
            //populate coords


            for (int i = -width / 2; i < width / 2; i++)
            {
                for (int j = -width / 2; j < width / 2; j++)
                {
                    //coords are in range [-0.5, 0.5]
                    coords[(i + width / 2) * width + j + width / 2, 0] = 1.0 * i / width;
                    coords[(i + width / 2) * width + j + width / 2, 1] = 1.0 * j / width;
                }
            }
        }
    }
}
