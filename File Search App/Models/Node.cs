using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace File_Search_App.Models
{
    internal class Node<T> where T : SystemItem
    {
        private T value;
        private List<Node<T>> children;

        public Node(T value)
        {
            this.value = value;
            children = new List<Node<T>>();
        }

        public T GetValue()
        {
            return value;

        }

        public string GetValueName()
        {
            return value.GetName();
        }

        public string GetValuePath()
        {
            return value.GetPath();
        }

        public List<Node<T>> GetChildren()
        {
            return children;
        }

        public void AddChild(T value)
        {
            children.Add(new Node<T>(value));
        }

        public List<Node<T>> GetRecursiveChildren()
        {
           
            List<Node<T>> list = children;

            if (children.Count != 0)
            {
                foreach (Node<T> child in children.ToList())
                {
                    list.AddRange(child.GetRecursiveChildren());
                }
            }
     
            return list;
            
        }

        public string[] GetChildrenNames()
        {
            string[] names = new string[children.Count];

            for (int i = 0; i < children.Count; i++)
            {
                names[i] += children[i].value.GetName();
            }

            return names;
        }

        public string[] GetChildrenPaths()
        {
            string[] paths = new string[children.Count];

            for (int i = 0; i < children.Count; i++)
            {
                paths[i] += children[i].value.GetPath();
            }

            return paths;
        }

        public int GetChildrenCount()
        {
            return children.Count;
        }
    }
}
