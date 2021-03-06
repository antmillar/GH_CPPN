﻿using NumSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TopolEvo.NEAT;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;

namespace TopolEvo.Architecture
{
    public interface Model
    {
        Matrix<double> ForwardPass(Matrix<double> input);
    }

    public class Network : Model
    {
        int _inputCount;
        List<int> outputNodes = new List<int>();
        List<int> inputNodes = new List<int>();
        Genome _genome;
        public List<List<NEAT.ConnectionGene>> _layers;
        public List<List<int>> _layersEndNodes;
        public List<Matrix<double>> _layersMatrices;
        public List<List<int>> _layersStartNodes;
        public Dictionary<int, Vector<double>> activations;

        public Network(NEAT.Genome genome)
        {
            _inputCount = 0;
            _genome = genome;


            foreach(NEAT.NodeGene nodeGene in _genome.Nodes)
            {
                if(nodeGene._type == "input")
                {
                    _inputCount += 1;
                }
            }

            GenerateLayers();
        }

        /// <summary>
        /// calculates the layers for the network based on nodes and connections and generates them
        /// </summary>
        public void GenerateLayers()
        {
            //use hashset as don't want repeated nodes
            _layers = new List<List<NEAT.ConnectionGene>>();
            _layersEndNodes = new List<List<int>>();
            _layersStartNodes = new List<List<int>>();
            _layersMatrices = new List<Matrix<double>>(); 

            //initialise the currentNodes with the input nodes
            //keep track of the output node (only one allowed)
            foreach (NEAT.NodeGene nodeGene in _genome.Nodes)
            {
                if (nodeGene._type == "input")
                {
                    inputNodes.Add(nodeGene._id);
                }

                else if (nodeGene._type == "output")
                {
                    outputNodes.Add(nodeGene._id);
                }

            }

            //recursively creates new layers
            var output = MakeLayer(new HashSet<int>(inputNodes));

        }

        public HashSet<int> MakeLayer(HashSet<int> visitedNodes)
        {
            //could probably cache in here somehow?
            //add bias to each layer
            visitedNodes.Add(9999);

            var currentLayer = new List<NEAT.ConnectionGene>();
            var potentialNodes = new HashSet<int>();
            var nextNodes = new HashSet<int>();

            //find all connections starting from current nodes and their output nodes
            foreach (NEAT.ConnectionGene connectionGene in _genome.Connections)
            {
                if (visitedNodes.Contains(connectionGene.InputNode) & !visitedNodes.Contains(connectionGene.OutputNode))
                {
                    currentLayer.Add(connectionGene);

                    //add node ID to potential next layer, unless it's a bias connection
                    if (connectionGene.InputNode != 9999)
                    {
                        potentialNodes.Add(connectionGene.OutputNode);
                    }
                }
            }

            //finds nodes where all inputs are coming from the current nodes
            foreach (var id in potentialNodes)
            {
                var inputs = _genome.GetNodeByID(id)._inputs;

                //ensure all inputs into node are from the current nodes only
                if (visitedNodes.Intersect(inputs).Count() == inputs.Count())
                {
                    nextNodes.Add(id);
                }
            }

            //for each node, if ANY input is not in the visitedNodes, remove all of the connections to it.
            //keep only connections with inputs from current nodes
            currentLayer.RemoveAll(x => !nextNodes.Contains(x.OutputNode));


            //recursively traverse the network until next nodes is empty
            if (nextNodes.Count == 0)
            {
                return nextNodes;

            }
            else
            {
                //create a Matrix to represent the layer of connections
                //each column represents an output node
                //each node can have a variable number of inputs, so matrix will be sparse
                //for the height of columns will use the number of nodes in previous layer, lazy upper bound

                var previousNodes = currentLayer.Select(x => x.InputNode).Distinct().Count();

                var layerMatrix = Matrix<double>.Build.Dense(previousNodes, nextNodes.Count, 0);

                var nextNodeList = nextNodes.ToList();
                var nodeCountList = new int[nextNodeList.Count];

                foreach (var connection in currentLayer)
                {
                    var index = nextNodeList.IndexOf(connection.OutputNode);
                    layerMatrix[nodeCountList[index], index] = connection.Weight;
                    nodeCountList[index]++;
                }

                var startNodes = currentLayer.Select(x => x.InputNode).Distinct();

                _layersMatrices.Add(layerMatrix);

                _layers.Add(currentLayer);

                if (_layers[0].Count < 3)
                {
                    var test = " ";
                }

                _layersStartNodes.Add(startNodes.ToList());
                _layersEndNodes.Add(nextNodes.ToList());

                //combine all visited nodes together for next iteration
                visitedNodes.UnionWith(nextNodes);
                return MakeLayer(visitedNodes);
            }

        }

        /// <summary>
        /// Single Forward Propagation through the network.
        /// Returns a set of outputs 
        /// </summary>

        public Matrix<double> ForwardPass(Matrix<double> inputs)
        {
            if (_inputCount != inputs.ColumnCount) throw new IncorrectShapeException($"Network has {_inputCount} inputs, input data has shape {inputs.ColumnCount}");

            activations = new Dictionary<int, Vector<double>>();

            //copy x, y, (z) columns
            for (int i = 0; i < inputs.ColumnCount; i++)
            {
                activations[i] = inputs.Column(i);
            }

            var bias = Vector<double>.Build.Dense(inputs.RowCount, 10.0);

            //populate bias
            activations[9999] = bias;

            Matrix<double> layerOutputs = null;

            //loop over layers and calculate activations
            for (int i = 0; i < _layers.Count; i++)
            {
                layerOutputs = CalculateLayer(i, inputs.RowCount);
            }

            //need to get the activations from the output layers

            var outputCount = outputNodes.Count;
            var outputs = Matrix<double>.Build.Dense(inputs.RowCount, outputCount);

            for (int i = 0; i < outputCount; i++)
            {
                try
                {
                    //if activation can't be found it means the network couldn't be traversed, I that case output defaults to 0.0
                    outputs.SetColumn(i, activations[outputNodes[i]]);
                }
                catch
                {
                    return outputs;
                }
            }

            //last iteration of loop returns the outputs from the final layer
            var output = outputs;

            return output;
        }

        private Matrix<double> CalculateLayer(int layerNum, int rows)
        {
            Matrix<double> layerOutputs = null;

            //get incoming nodes
            var matrixInputs = Matrix<double>.Build.Dense(rows, _layersStartNodes[layerNum].Count, 0);

            //create matrix of inputs for the layer
            for (int j = 0; j < _layersStartNodes[layerNum].Count; j++)
            {
                int nodeNum = _layersStartNodes[layerNum][j];
                matrixInputs.SetColumn(j, activations[nodeNum]);
            }

            //layer w*x + b
            layerOutputs = matrixInputs * _layersMatrices[layerNum];

            //apply activation functions to each column (node)

            Parallel.For(0, layerOutputs.ColumnCount, (j) =>
            {
                var nodeNum = _layersEndNodes[layerNum][j];
                var actFunction = _genome.GetNodeByID(nodeNum).Activation.Function;

                var newVals = layerOutputs.Column(j);
                layerOutputs.Column(j).Map(actFunction, newVals, Zeros.Include);
                layerOutputs.SetColumn(j, newVals);


            });

            //have to populate dictionary outside the parallel for because not thread safe
            for (int j = 0; j < layerOutputs.ColumnCount; j++)
            {
                var nodeNum = _layersEndNodes[layerNum][j];
                activations[nodeNum] = layerOutputs.Column(j);
            }

            return layerOutputs;
        }
    }

    
    public class Activation
    {
        public Func<double, double> Function { get; set; }
        public string Name { get; set; }

        public Activation(Func<double, double> function, string name)
         {
            Function = function;
            Name = name;
          }
    }

    public static class Activations
    {
        public static Activation Sigmoid()
        {
            return new Activation(x => (1 / (1 + Math.Exp(-5 * x))), "Sigmoid");
        }

        public static Activation Tanh()
        {
            return new Activation(Trig.Tanh, "Tanh");
        }
        internal static Activation TanhAbs()
        {
            return new Activation((value) => Math.Abs(Trig.Tanh(value)), "TanhAbs");
        }
        public static Activation Sin()
        {
            return new Activation(Trig.Sin, "Sin");
        }
        public static Activation Fract()
        {
            return new Activation((value) => (value - Math.Truncate(value)), "Fract");
        }

        internal static Activation Rescale()
        {
            return new Activation((value) => (5.0 * value), "Rescale");
        }
        internal static Activation Downscale()
        {
            return new Activation((value) => (0.2 * value), "Downscale");
        }
        internal static Activation Gaussian()
        {
            return new Activation((value) => 0.4 *  Math.Exp(-0.5 * (value * value)), "Gaussian");
        }

        internal static Activation Square()
        {
            return new Activation((value) => (value * value), "Square");
        }

        internal static Activation Abs()
        {
            return new Activation((value) => Math.Abs(value), "Abs");
        }
        internal static Activation Cos()
        {
            return new Activation(Trig.Cos, "Cos");
        }

        internal static Activation Linear()
        {
            return new Activation((value) => (value), "Linear");
        }

        internal static Activation Random()
        {
            return new Activation((value) => (new System.Random((int) value).NextDouble()), "Random");
        }
    }
    }
