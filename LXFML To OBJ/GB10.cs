using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace LXFML_To_OBJ
{
    class GB10
    {
        private List<Mesh> Meshes;
        private string ID;
        private Color Material;

        public GB10(string fileName, string targetID, string materialID)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException();

            if (!fileName.EndsWith(".lif"))
                throw new InvalidDataException();

            //LIF Header
            // 4 bytes | char[4] | File magic: "LIFF"
            // 4 bytes | uint32  | Padding
            // 4 bytes | uint32  | Total file size
            // 2 bytes | uint16  | The number 1
            // 4 bytes | uint32  | Padding

            //LIF Section Types
            // Type 1: File Header, contains size and data of entire file
            // Type 2: Contains the file's data
            // Type 3: Folder, Hierarchy of type 3 and 4 blocks
            // Type 4: File, Section data = file data
            // Type 5: File and Folder names, Directory tree of files

            //LIF Sections Structure
            // 2 bytes | uint16 | Section start: Always 1
            // 2 bytes | uint16 | Section type: 1-5
            // 4 bytes | uint32 | Padding
            // 4 bytes | uint32 | Section size in bytes + 20 bytes
            // 4 bytes | uint32 | Padding: Value of 1 for section types 2, 4, 5
            // 4 bytes | uint32 | Padding
            // n bytes | uint32 | Section data

            //Folder Node Structure
            // 2 bytes | uint16   | Node type: 1
            // 4 bytes | uint32   | Unknown: can be 0 or 7 (0 for root folder)
            // N bytes | char16[] | Folder name
            // 4 bytes | uint32   | Padding
            // 4 bytes | uint32   | Section size
            // 4 bytes | uint32   | Amount of sub-nodes

            //File Node Structure
            // 2 bytes | uint16   | Node type: 2
            // 4 btyes | uint32   | Unknown: can be 0 or 7
            // N bytes | char16[] | File name
            // 4 bytes | uint32   | Padding
            // 4 bytes | uint32   | Section size (File size = Section Size - 20)
            // 8 bytes | long     | Created, modified or accessed date
            // 8 bytes | long     | Created, modified or accessed date
            // 8 bytes | long     | Created, modified or accessed date



            //-------------------------------------
            //----- g IDs start at 0x1DB977A1 -----
            //-------------------------------------

            string[] ids = new string[2897]; //Array to store each id
            using (EndianReader reader = new EndianReader(new FileStream(fileName, FileMode.Open), Endian.Big))
            {
                reader.ReadBytes(0x1DB977A1);
                for (int i = 0; i < ids.Length; i++)
                {
                    StringBuilder id = new StringBuilder();
                    char c = (char)reader.ReadInt16();
                    while (c != '\u0000')
                    {
                        id.Append(c);
                        c = (char)reader.ReadInt16();
                    }

                    ids[i] = Path.GetFileNameWithoutExtension(id.ToString());

                    reader.ReadBytes(38); //Jump to next id
                }
            }



            //----------------------------------
            //----- Get offset of targets ------
            //----------------------------------

            int[] keys = ids.Select((n, i) => n == targetID ? i : -1).Where(i => i != -1).ToArray();

            for(int i = 0; i < keys.Length; i++)
            {
                keys[i] = i > 0 ? 0 : keys[i];
            }

            if (keys.Length == 0)
            {
                throw new InvalidDataException("LXFML Model ID not recognized");
            }
            else
            {
                ID = targetID;
            }



            //--------------------------------------
            //----- g files start at 0x108F34B -----
            //--------------------------------------

            using (EndianReader reader = new EndianReader(new FileStream(fileName, FileMode.Open), Endian.Big))
            {
                Meshes = new List<Mesh>();

                reader.ReadBytes(0x108F34B);
                for (int j = 0; j < keys.Length; j++)
                {
                    //Skip over padding
                    reader.ReadBytes(8);

                    for (int k = 0; k < keys[j]; k++)
                    {
                        reader.ReadBytes(reader.ReadInt32() - 4);
                    }

                    int targetSize = reader.ReadInt32() - 20;
                    Console.WriteLine(targetSize);

                    //Skip over padding
                    reader.ReadBytes(8);

                    //Creates new GB10 object from file data
                    Console.WriteLine(reader.BaseStream.Position);
                    Meshes.Add(new Mesh(reader.ReadBytes(targetSize)));
                }
            }



            //----------------------------------------------
            //----- Material xml begins at 0x1DB102E4 -----
            //----------------------------------------------

            using (EndianReader reader = new EndianReader(new FileStream(fileName, FileMode.Open), Endian.Big))
            {
                reader.ReadBytes(0x1DB102E4);

                using (XmlReader xreader = XmlReader.Create(new MemoryStream(reader.ReadBytes(16366))))
                {
                    xreader.ReadToFollowing("Material");
                    do
                    {
                        if (xreader.GetAttribute("MatID") == materialID)
                        {
                            int r = Convert.ToInt32(xreader.GetAttribute("Red"));
                            int g = Convert.ToInt32(xreader.GetAttribute("Green"));
                            int b = Convert.ToInt32(xreader.GetAttribute("Blue"));
                            int a = Convert.ToInt32(xreader.GetAttribute("Alpha"));
                            Material = Color.FromArgb(a, r, g, b);

                            break;
                        }
                    }
                    while (xreader.ReadToFollowing("Material"));
                }
            }
        }

        public void Offset(float x, float y, float z)
        {
            foreach(Mesh m in Meshes)
            {
                m.Offset(x, y, z);
            }
        }

        public void Rotate(float angle, float ax, float ay, float az)
        {
            foreach(Mesh m in Meshes)
            {
                m.Rotate(angle, ax, ay, az);
            }
        }

        public void SetTexture(string fileName, string textureID)
        {
            foreach(Mesh m in Meshes)
            {
                if (m.HasUV)
                {
                    m.SetTexture(fileName, textureID);
                }
            }
        }

        public static void WriteOBJ(List<GB10> gs, string path)
        {
            //-------------------------------------
            //----- Account for duplicate IDs -----
            //-------------------------------------

            for(int i = 0; i < gs.Count;)
            {
                for(int j = 0; j < gs.FindAll(delegate(GB10 g) { return g.ID == gs[i - j].ID; }).Count; j++, i++)
                {
                    gs[i].ID += j > 0 ? (j / 1000f).ToString() : "";
                }
            }

            using (StreamWriter writer = new StreamWriter(Path.Combine(path)))
            {
                int triangleCount = 0;

                foreach (GB10 g in gs)
                {
                    //-------------------------
                    //----- object header -----
                    //-------------------------

                    //writer.WriteLine("o " + g.ID);

                    for (int i = 0; i < g.Meshes.Count; i++)
                    {
                        //-----------------------
                        //----- mesh header -----
                        //-----------------------

                        writer.WriteLine("o " + g.ID + "." + i);



                        //--------------------
                        //----- vertices -----
                        //--------------------

                        for (int j = 0; j < g.Meshes[i].Vertices.GetLength(0); j++)
                        {
                            for (int k = 0; k < g.Meshes[i].Vertices.GetLength(1); k += 3)
                            {
                                float v1 = g.Meshes[i].Vertices[j, k];
                                float v2 = g.Meshes[i].Vertices[j, k + 1];
                                float v3 = g.Meshes[i].Vertices[j, k + 2];
                                writer.WriteLine("v " + v1.ToString("n6") + " " + v2.ToString("n6") + " " + v3.ToString("n6"));
                            }
                        }



                        //---------------
                        //----- uvs -----
                        //---------------

                        for (int j = 0; j < g.Meshes[i].UV.GetLength(0); j++)
                        {
                            for (int k = 0; k < g.Meshes[i].UV.GetLength(1); k += 2)
                            {
                                float u1 = g.Meshes[i].UV[j, k];
                                float u2 = g.Meshes[i].UV[j, k + 1];
                                writer.WriteLine("vt " + u1.ToString("n6") + " " + u2.ToString("n6"));
                            }
                        }



                        //-------------------
                        //----- normals -----
                        //-------------------

                        for (int j = 0; j < g.Meshes[i].Normals.GetLength(0); j++)
                        {
                            for (int k = 0; k < g.Meshes[i].Normals.GetLength(1); k += 3)
                            {
                                float n1 = g.Meshes[i].Normals[j, k];
                                float n2 = g.Meshes[i].Normals[j, k + 1];
                                float n3 = g.Meshes[i].Normals[j, k + 2];
                                writer.WriteLine("vn " + n1.ToString("n6") + " " + n2.ToString("n6") + " " + n3.ToString("n6"));
                            }
                        }



                        //------------------------------
                        //----- material reference -----
                        //------------------------------

                        writer.WriteLine("usemtl " + g.ID + "." + i + "-mat");



                        //---------------------
                        //----- triangles -----
                        //---------------------

                        writer.WriteLine("s 1");
                        for (int j = 0; j < g.Meshes[i].Tris.Length; j += 3)
                        {
                            float t1 = g.Meshes[i].Tris[j] + 1 + triangleCount;
                            string tri1 = t1 + "/" + t1 + "/" + t1;

                            float t2 = g.Meshes[i].Tris[j + 1] + 1 + triangleCount;
                            string tri2 = t2 + "/" + t2 + "/" + t2;

                            float t3 = g.Meshes[i].Tris[j + 2] + 1 + triangleCount;
                            string tri3 = t3 + "/" + t3 + "/" + t3;

                            writer.WriteLine("f " + tri1 + " " + tri2 + " " + tri3);
                        }
                        triangleCount += g.Meshes[i].Vertices.GetLength(0);
                    }
                }
            }



            //------------------------------------
            //----- Save any textures as PNG -----
            //------------------------------------

            string dir = Path.GetDirectoryName(path);
            foreach (GB10 g in gs)
            {
                foreach(Mesh m in g.Meshes)
                {
                    if (!(m.Texture == null) && !string.IsNullOrEmpty(m.TextureID))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            m.Texture.Save(ms, ImageFormat.Png);
                            var img = Image.FromStream(ms);

                            img.Save(Path.Combine(dir, m.TextureID + ".png"));
                        }
                    }
                }
            }



            //--------------------------------
            //----- Save Material as MTL -----
            //--------------------------------

            using (StreamWriter writer = new StreamWriter(Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + ".mtl"), true))
            {
                foreach(GB10 g in gs)
                {
                    float red = g.Material.R / 255f;
                    float green = g.Material.G / 255f;
                    float blue = g.Material.B / 255f;
                    float alpha = g.Material.A / 255f;

                    for(int i = 0; i < g.Meshes.Count; i++)
                    {
                        //-------------------------
                        //----- Material Name -----
                        //-------------------------

                        writer.WriteLine("newmtl " + g.ID + "." + i + "-mat");

                        //--------------------------
                        //----- Ambient Light ------
                        //--------------------------

                        writer.WriteLine("Ka " + red.ToString("n6") + " " + green.ToString("n6") + " " + blue.ToString("n6"));

                        //-------------------------
                        //----- Diffuse Color -----
                        //-------------------------

                        writer.WriteLine("Kd " + red.ToString("n6") + " " + green.ToString("n6") + " " + blue.ToString("n6"));

                        //--------------------------
                        //----- Specular Color -----
                        //--------------------------

                        writer.WriteLine("Ks 0.333333 0.333333 0.333333");

                        //-------------------------------
                        //----- Specular Highlights -----
                        //-------------------------------

                        writer.WriteLine("Ns 0");

                        //--------------------
                        //----- Dissolve -----
                        //--------------------

                        writer.WriteLine("d " + alpha.ToString("n6"));

                        //------------------------
                        //----- Texture File -----
                        //------------------------

                        if (!(g.Meshes[i].Texture == null) && !string.IsNullOrEmpty(g.Meshes[i].TextureID))
                        {
                            writer.WriteLine("map_Kd " + g.Meshes[i].TextureID + ".png");
                        }
                    }
                }
            }
        }
    }
}
