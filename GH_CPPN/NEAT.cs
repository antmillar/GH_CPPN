﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPPN.NEAT
{
   
    public static class Config
    {
        public static System.Random globalRandom = new System.Random();
        public static double mutateRate = 0.2;
        public static string fitnessTarget = "min";
        
    }

    public class Population
    {

        protected internal List<Genome> Genomes { get; set; }

        public Population(int size)
        {
            Genomes = new List<Genome>();

            for (int i = 0; i < size; i++)
            {
                Genomes.Add(new Genome());
            }
        }

        //currently has to be run after the fitnesses are calculated
        public void NextGeneration()
        {
            for (int i = Genomes.Count / 2; i < Genomes.Count; i++)
            {
                Genomes[i] = new Genome(Genomes[i - Genomes.Count/2]);
            }

            foreach (var genome in Genomes)
            {
                //could create a copy in here instead and return it
                genome.Mutate();
            }

            SortByFitness();
        }

        public void SortByFitness()
        {
            if (Config.fitnessTarget == "min")
            {
                Genomes = Genomes.OrderBy(x => x.Fitness).ToList();
            }
            else if (Config.fitnessTarget == "max")
            {
                Genomes = Genomes.OrderByDescending(x => x.Fitness).ToList();
            }
        }

        public override string ToString()
        {
            return String.Join("\n", Genomes.Select(x => x.ToString()));
        }

        public string PrintInputs()
        {
            string s = "Node Inputs : " + String.Join(" ", Genomes.Select(x => x.ToString()));

            return s;
        }

    }
         
    public class Gene { };

    public class NodeGene : Gene
    {
        protected internal string _activationType;
        protected internal int _id;
        protected internal string _type;
        protected internal List<int> _inputs;
        protected internal double _bias;

        public NodeGene(int id, string type, string activationType = "tanh")
        {
            _id = id;
            _type = type;
            _inputs = new List<int>();
            _activationType = activationType;
            _bias = Config.globalRandom.NextDouble() * 2 - 1.0;
        }

        //copy constructor
        public NodeGene(NodeGene parent)
        {
            _id =  parent._id;
            _type = parent._type;
            _inputs = parent._inputs;
            _activationType = parent._activationType;
            _bias = parent._bias;

        }

        public override string ToString()
        {
            return _id.ToString();
        }

    }
    public class ConnectionGene : Gene
    {
        protected internal double Weight { get; set; }
        protected internal int _inputNode;
        protected internal int _outputNode;

        public ConnectionGene(int inputNode, int outputNode)
        {
            _inputNode = inputNode;
            _outputNode = outputNode;

            Weight = Config.globalRandom.NextDouble() * 2 - 1.0;
        }

        //copy constructor
        public ConnectionGene(ConnectionGene parent)
        {
            _inputNode = parent._inputNode;
            _outputNode = parent._outputNode;

            Weight = parent.Weight;
        }

        public override string ToString()
        {
            return $"({_inputNode}, {_outputNode}) w = {Math.Round(Weight, 2)} ";
        }
    }


    public class Genome
    {
        public List<NodeGene> Nodes { get; set; }
        public List<ConnectionGene> Connections { get; set; }
        protected internal double Fitness { get; set; }

        public Genome()
        {
            //initialise a standard architecture
            Nodes = new List<NodeGene>();
            Connections = new List<ConnectionGene>();
            Fitness = 0.0;

            Nodes.Add(new NodeGene(0, "input"));
            Nodes.Add(new NodeGene(1, "input"));
            Nodes.Add(new NodeGene(2, "hidden"));
            Nodes.Add(new NodeGene(3, "hidden"));
            Nodes.Add(new NodeGene(4, "hidden"));
            Nodes.Add(new NodeGene(5, "hidden"));
            Nodes.Add(new NodeGene(6, "output", "sigmoid"));

            Connections.Add(new ConnectionGene(0, 2));
            Connections.Add(new ConnectionGene(0, 3));
            Connections.Add(new ConnectionGene(0, 4));
            Connections.Add(new ConnectionGene(0, 5));

            Connections.Add(new ConnectionGene(1, 2));
            Connections.Add(new ConnectionGene(1, 3));
            Connections.Add(new ConnectionGene(1, 4));
            Connections.Add(new ConnectionGene(1, 5));

            Connections.Add(new ConnectionGene(2, 6));
            Connections.Add(new ConnectionGene(3, 6));
            Connections.Add(new ConnectionGene(4, 6));
            Connections.Add(new ConnectionGene(5, 6));

            CalculateInputs();

        }

        //copy constructor
        public Genome(Genome parent)
        {
            Nodes = new List<NodeGene>();
            Connections = new List<ConnectionGene>();
            Fitness = parent.Fitness;

            foreach(var node in parent.Nodes)
            {
                Nodes.Add(new NodeGene(node));
            }

            foreach(var conn in parent.Connections)
            {
                Connections.Add(new ConnectionGene(conn));
            }
        }

        public NodeGene GetNodeByID(int id)
        {
            return Nodes.Single(x => x._id == id);
        }

        //populate the list of input node ids into each node
        private void CalculateInputs()
        {
            foreach(NodeGene nodeGene in Nodes)
            {
                foreach(ConnectionGene connection in Connections)
                {
                    if(connection._outputNode == nodeGene._id)
                    {
                        nodeGene._inputs.Add(connection._inputNode);
                    }
                }
            }
        }

        //mutates just the connection genes currently
        public void Mutate()
        {
            foreach(ConnectionGene connection in Connections)
            {
                if (Config.globalRandom.NextDouble() < Config.mutateRate)
                {
                    //need to clamp weights?
                    connection.Weight += (Config.globalRandom.NextDouble() - 0.5) / 2.0;
                }
            }
        }

        public void CrossOver(Genome other)
        {


        }

        public override string ToString()
        {
            string s = "Nodes : " + String.Join(" ", Nodes.Select(x => x.ToString()));
            s += " | Conns : " + String.Join(" ", Connections.Select(x => x.ToString()));

            return s;
        }

    }
    
}
