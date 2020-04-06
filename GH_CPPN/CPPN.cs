﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using NumSharp;

//custom libraries
using CPPN.NEAT;
using CPPN.Net;
using CPPN.Display;
using CPPN.Fitness;

namespace GH_CPPN
{
    public class CPPN : GH_Component
    {

        public CPPN() : base("CPPN", "CPPN", "Constructing a 2d CPPN", "CPPN", "Simple")
            {

            }

        public override Guid ComponentGuid
        {
            // Don't copy this GUID, make a new one
            get { return new Guid("ab047315-77a4-45ef-8039-44cd6428b913"); }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            //pManager.AddTextParameter("input text", "i", "string to reverse", GH_ParamAccess.item);
            //pManager.AddNumberParameter("input number", "num", "number to input", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            //pManager.AddPointParameter("points", "pts", "grid points", GH_ParamAccess.list);
            pManager.AddMeshParameter("mesh grid", "mg", "grid of meshes", GH_ParamAccess.list);
            //pManager.AddNumberParameter("outputs", "outputs", "output of linear", GH_ParamAccess.list);

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            //double data = double.NaN;
            //if (!DA.GetData(0, ref data)) { return; }
            //if (data == double.NaN) { return; }

            //var linear = new temp.CustomModel();
            //NDArray activations = linear.ForwardPass(coords);

            //var mlp = new temp.MLP(2, 1, 8);
            //NDArray activations = mlp.ForwardPass(coords);

            int popSize = 10;
            var pop = new Population(popSize);
            var genome = new Genome();

            List<Network> nets = new List<Network>();


            for (int i = 0; i < popSize; i++)
            {
                nets.Add(new Network(pop.Genomes[i]));
            }
            //var network = new Network(genome);



            int width = 20;

            //populate coords
            NDArray coords = np.ones((width * width, 2));

            for (int i = -width / 2; i < width / 2; i++)
            {
                for (int j = -width / 2; j < width / 2; j++)
                {
                    //coords are in range [-0.5, 0.5]
                    coords[(i + width / 2) * width + j + width / 2, 0] = 1.0 * i / width;
                    coords[(i + width / 2) * width + j + width / 2, 1] = 1.0 * j / width;
                }
            }

            var outputs = new List<NDArray>();
            var meshes = new List<Mesh>();

            foreach (var net in nets)
            {
                var output = net.ForwardPass(coords);
                outputs.Add(output);
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                var drawing = new Drawing(width, -popSize/2 * width + i * width, 0);
                Mesh combinedMesh = drawing.Paint(outputs[i]);
                meshes.Add(combinedMesh);
            }

            var fits = Fitness.Function(pop, coords);

            //paint mesh using outputs


            //output data from GH component
            DA.SetDataList(0, meshes);
            //DA.SetDataList(1, output);
        }
    }
}
