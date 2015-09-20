﻿using System;
using System.Collections.Generic;
using System.Reflection;

using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Roboto
{
    

    public class settings
    {
        private static string foldername = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Roboto\";
        private static string filename = foldername + "settings.xml";

        //module list. Static, as dont want to serialise the plugins, just the data.
        public static List<Modules.RobotoModuleTemplate> plugins = new List<Modules.RobotoModuleTemplate>(); 
        
        public List<replacement> replacements = new List<replacement>();

        public string telegramAPIURL;
        public string telegramAPIKey;
        public string botUserName = "";
        public int waitDuration = 60; //wait duration for long polling. 
        public int lastUpdate = 0; //last update index, needs to be passed back with each call. 
        
        //generic plugin storage. NB: Chats DO want to be serialised. 
        public List<Modules.RobotoModuleDataTemplate> pluginData = new List<Modules.RobotoModuleDataTemplate>();
        public List<chat> chatData = new List<chat>();

        //stuff
        static Random randGen = new Random();

        /// <summary>
        /// Load all the plugins BEFORE loading the settings file. We need to be able to enumerate the extra types when loading the XML. 
        /// </summary>
        public static void loadPlugins()
        {
            //load all plugins by looking for all objects derived from the abstract class. 
            Assembly currAssembly = Assembly.GetExecutingAssembly();

            foreach (Type type in currAssembly.GetTypes())
            {
                if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(Modules.RobotoModuleTemplate)))
                {
                    Console.WriteLine("Registering plugin " + type.Name);

                    if (pluginExists(type))
                    {
                        //TODO - this is going to be looking for the template, not the datatemplate!
                        //Console.WriteLine("Registering plugin " + type.Name);
                    }
                    else
                    {
                        Modules.RobotoModuleTemplate plugin = (Modules.RobotoModuleTemplate)Activator.CreateInstance(type);
                        Console.WriteLine("Added " + plugin.GetType().ToString());
                        plugins.Add(plugin);
                        plugin.init();
                    }

                }
            }
        }


        /// <summary>
        /// Basic checks on the data. 
        /// </summary>
        public void validate()
        {
            foreach (Modules.RobotoModuleTemplate plugin in plugins)
            {
                plugin.initData(); //this data probably already exists if loaded by XML, but if not, allow the plugin to create it. 
                if (plugin.pluginDataType != null)
                {
                    //TODO - check if this datatype is a subclass of RobotoModuleDataTemplate
                }
                //TODO - do same for chat data types. 
            }


            if (telegramAPIURL == null) {telegramAPIURL = "https://api.telegram.org/bot";};
            if (telegramAPIKey == null) { telegramAPIKey = "ENTERYOURAPIKEYHERE"; };
            if (botUserName == "") { botUserName = "Roboto_bot_name"; }

            Console.WriteLine("=========");
            Console.WriteLine("All Plugins initialised");
            Console.WriteLine(Modules.mod_standard.getAllMethodDescriptions());
            Console.WriteLine("=========");


            foreach (chat c in chatData)
            {
                c.initPlugins();
            }

        }

        /// <summary>
        /// Load all our data from XML
        /// </summary>
        /// <returns></returns>
        public static settings load()
        {

            try
            {

                XmlSerializer deserializer = new XmlSerializer(typeof(settings), getPluginDataTypes());
                TextReader textReader = new StreamReader(filename);
                settings setts = (settings)deserializer.Deserialize(textReader);
                textReader.Close();
                return setts;
            }


            catch (Exception e)
            {
                if (e is System.IO.FileNotFoundException || e is System.IO.DirectoryNotFoundException)
                {
                    //create a new one
                    settings sets = new settings();

                    return sets;
                }
                else
                {
                    Console.WriteLine(e.ToString());
                }
            }
            return null;

        }

        /// <summary>
        /// Get all the custom types used, for serialising / deserialising data to XML.
        /// </summary>
        /// <returns></returns>
        public static Type[] getPluginDataTypes()
        {
            //put into a list first
            List<Type> customTypes = new List<Type>();
            foreach (Modules.RobotoModuleTemplate plugin in plugins)
            {
                if (plugin.pluginDataType != null) { customTypes.Add(plugin.pluginDataType);}
                if (plugin.pluginChatDataType != null) { customTypes.Add(plugin.pluginChatDataType); }
            }
            
            return customTypes.ToArray();
        }

        /// <summary>
        /// Save all data to XML
        /// </summary>
        public void save()
        {


            XmlSerializer serializer = new XmlSerializer(typeof(settings), getPluginDataTypes() );

            //create folder if doesnt exist:
            DirectoryInfo di = new DirectoryInfo(foldername);
            if (!di.Exists)
            {
                di.Create();
            }

            TextWriter textWriter = new StreamWriter(filename);
            serializer.Serialize(textWriter, this);
            textWriter.Close();
        }



        public static int getRandom(int maxInt)
        {
            return randGen.Next(maxInt);
        }



        

        public int getUpdateID()
        {
            return lastUpdate + 1;
        }

        public void registerData(Modules.RobotoModuleDataTemplate data)
        {

            if (typeDataExists(data.GetType()) == false)
            {
                pluginData.Add(data);
                Console.WriteLine("Added data of type " + data.GetType().ToString());
            }
            else
            {
                Console.WriteLine("Plugin data of type " + data.GetType().ToString() + " already exists!");
            }

        }

        /// <summary>
        /// Check if a plugins datastore exists
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool typeDataExists(Type t)
        {
            bool found = false;
            foreach (Modules.RobotoModuleDataTemplate existing in pluginData)
            {
                if (t.GetType() == existing.GetType())
                {
                    
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// check if a plugin Type exists
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool pluginExists(Type t)
        {
            bool found = false;
            foreach (Modules.RobotoModuleTemplate existing in plugins)
            {
                if (t.GetType() == existing.GetType())
                {

                    found = true;
                }
            }
            return found;
        }

        public T getPluginData<T>()
        {
            foreach (Modules.RobotoModuleDataTemplate existing in pluginData)
            {
                if (existing.GetType() == typeof(T))
                {
                    //Console.WriteLine("Plugin data of type " + data.GetType().ToString() + " already exists!");
                    T retVal = (T) Convert.ChangeType(existing, typeof(T));
                    return retVal;
                }
            }

            Console.WriteLine("Couldnt find plugin data of type " + typeof(T).ToString());
            throw new InvalidDataException("Couldnt find plugin data of type " + typeof(T).ToString());
            
        }


        public Modules.RobotoModuleDataTemplate getPluginData(Type pluginDataType)
        {
            foreach (Modules.RobotoModuleDataTemplate existing in pluginData)
            {
                if (existing.GetType() == pluginDataType)
                {
                    return existing;
                }
            }

            Console.WriteLine("Couldnt find plugin data of type " + pluginDataType.ToString());
            throw new InvalidDataException("Couldnt find plugin data of type " + pluginDataType.ToString());
        }

        /// <summary>
        /// find a chat by its chat ID
        /// </summary>
        /// <param name="chat_id"></param>
        /// <returns></returns>
        public chat getChat(int chat_id)
        {
            foreach (chat c in chatData)
            {
                if (c.chatID == chat_id)
                {
                    return c;
                }
            }
            return null;
        }

        /// <summary>
        /// Add data about a chat to the store. 
        /// </summary>
        /// <param name="chat_id"></param>
        public chat addChat(int chat_id)
        {
            if (getChat(chat_id) == null)
            {
                Console.WriteLine("Creating data for chat " + chat_id.ToString());
                chat chatObj = new chat(chat_id);
                chatData.Add(chatObj);
                return chatObj;
            }
            else
            {
                throw new InvalidDataException("Chat already exists!");
            }
        }

    }

}
