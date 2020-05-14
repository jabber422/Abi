using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Abi
{
    class Program
    {
        //sign cli entry point
        static int Main(string[] args)
        {
            String contextJson = args[0];
            RunTimeContext context = new RunTimeContext(contextJson);
            ToolManagerFactory tm = new ToolManagerFactory("path to manifest.xml");

            //abstract AbiTool is concrete signFileTool
            AbiTool signFileTool = tm.GetTool("SignFile");

            //factory makes commands for tools
            //signFile -> sign
            //signFile -> validate
            //KW -> scan
            //KW -> upload
            //...
            AbiToolCommandFactory abiToolCmdFactory = new AbiSignFileCommandFactory(signFileTool, context.getByName("Signing"));
            
            //abstract command "is a" concrete sign command
            AbiCmd signCmd = abiToolCmdFactory.CreateCommand("sign");
            
            //abstract result "is a" sign command result
            AbiCmdResult result = signCmd.DoWork("sign target in context");

            //generate the output context from the current AbiCmdResult
            result.getContext().WriteToFile();
            
            //pass result return code to user
            return result.code;
        }
    }

    #region context objects
    public class RunTimeContext: ISerializable
    {
        Dictionary<string, ContextObject> jsonMap;
        public RunTimeContext(String jsonPath)
        {
            jsonMap = new Dictionary<string, ContextObject>();
            //decode json path -> jsonMap
        }

        internal ContextObject getByName(string p)
        {
 	        throw new NotImplementedException();
        }

        internal void WriteToFile()
        {
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class ContextObject
    {
        //if there are multiple context objects, gets a specific on.
        //is two blocks of Signing context, get one of the other
        abstract public ContextObject GetTarget(string target);
    }

    public class SignContext : ContextObject, ISerializable
    {
        public Tuple<string, string> hash_algo;
        public Tuple<string, string> cert;
        
        public SignContext()
        {
            hash_algo = new Tuple<string, string>("--hash", "sha256");
            cert = new Tuple<string, string>("--cert", "OWR-EV-20");
        }

        public override ContextObject GetTarget(string target)
        {
            throw new NotImplementedException();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region Manifest
    //read/write interface for a manifest.xml file
    //psuedo code
    public class Manifest
    {
        Dictionary<object, object> manifest;
        public Manifest(string pathToManifestFile)
        {
            manifest = new Dictionary<object,object>();
            //decode manifest path -> manifestMap
        }

        //given a name get the item type
        public ManifestEntry getEntry(string name)
        {
            var v = this.manifest.First(n => n.Key == name).Value;
            return new ManifestEntry(v);
        }
    }

    //represents a property? block in the Manifest.xml
    public class ManifestEntry
    {
        string name;
        string type;
        //...

        public ManifestEntry(object v)
        {
            //v is block of xml code -> <project/> block
        }

        //accesor methods to xml properties/fields forget the correct term
        public string getProperty()
        {
            return "prop";
        }

        public string getAnnotations()
        {
            return "anno";
        }

        public string getVersion()
        {
            return "1.0";
        }
    }

    #endregion

    #region Tool Manager
    //this is a factory class that generates tool objects.
    public class ToolManagerFactory
    {
        Manifest manifest;
        public ToolManagerFactory(string manifest_xml)
        {
            //validate manifest.xml exists
            this.manifest = new Manifest(manifest_xml);
        }

        public AbiTool GetTool(string toolName)
        {
            //reads manifest.xml
            ManifestEntry manifestEntry = this.manifest.getEntry(toolName);
            //fetches tool if needed
            if(!this.DownloadTool(manifestEntry))
            {
                //error
            }

            //returns tool object
            return CreateTool(toolName, manifestEntry);
        }

        public bool DownloadTool(ManifestEntry manifestEntry)
        {
            //use wit to download the tool
            return true;
        }

        public AbiTool CreateTool(string toolName, ManifestEntry manifestEntry)
        {
            AbiTool result = null;
            switch (toolName)
            {
                //gonna be a huge nasty switch block, Activator would make this simpler but reflection can make heads blow up
                case "SignFile" : 
                    //make the generic SignFile
                    result = new SignFile(this.GetToolPath(manifestEntry));
                    break;
                case "SignTool":
                    //make the generic SignFile
                    result = new SignTool(this.GetToolPath(manifestEntry));
                    break;
                default:
                    throw new Exception("Unsupported tool name!");
            }
            return result;
        }

        public string GetToolPath(ManifestEntry manifestEntry)
        {
            return "path to tool exe";
        }
    }

    //tool as a component
    public abstract class AbiTool
    {
        //every tool is versioned
        public string version;
        public abstract AbiCmdResult DoWork(AbiCmd cmd);
    }

    //this tool uses the cli to work
    public abstract class CliTool : AbiTool
    {
        //all abi tools have an exe
        protected ProcessStartInfo psi;

        public CliTool(string path)
        {
            psi = new ProcessStartInfo(path);
        }
    }

    //this tool uses a rest api to work
    public abstract class RestTool : AbiTool
    {
        //all abi tools have an exe
        //use what ever the C3 url/rest objects is called
        string endpoint;

        public RestTool(string url)
        {
            this.endpoint = url;
        }
    }
    #endregion

    #region SignFile impl
    //sign file "is a" abi tool
    //this was the first time we made signFile and the original impl
    //this would enforce the known interface for SingFile version 0
    public class SignFile : CliTool
    {
        public SignFile(string path): base(path)
        {
        }

        public override AbiCmdResult DoWork(AbiCmd cmd)
        {
            //convert AbiCmd to CLI args
            this.psi.Arguments = cmd.GetArgsString();
            Process p = Process.Start(psi);
            p.WaitForExit();
            
            //read stdout
            return new SignCmdResult(p, cmd.context);
        }
    }

    public class SignTool : CliTool
    {
        public SignTool(string path)
            : base(path)
        {
        }

        public override AbiCmdResult DoWork(AbiCmd cmd)
        {
            //convert AbiCmd to CLI args
            this.psi.Arguments = cmd.GetArgsString();
            Process p = Process.Start(psi);
            p.WaitForExit();

            //read stdout
            return new SignCmdResult(p, cmd.context);
        }
    }
    #endregion

    #region abstract tool command factory
    //abstract factory
    public abstract class AbiToolCommandFactory
    {
        internal AbiTool tool;
        internal ContextObject context;
        public AbiToolCommandFactory(AbiTool tool, ContextObject context)
        {
            this.tool = tool;
            this.context = context;
        }

        public abstract AbiCmd CreateCommand(string cmd);
    }
    #endregion

    #region Abi tool command
    //an Abi command "has a" AbiTool
    //implements a single usage of an abi tool
    public abstract class AbiCmd
    {
        internal AbiTool tool;
        internal ContextObject context;

        ProcessStartInfo psi;
        public AbiCmd(AbiTool tool, ContextObject context)
        {
            this.tool = tool;
            this.context = context;
        }

        public abstract AbiCmdResult DoWork(string args);
        public abstract List<Tuple<string, string>> GetArgTuples();
        public abstract string GetArgsString();
    }

    //decorator allow new 'usages' to overload previous usages
    public abstract class AbiCmdDecorator : AbiCmd
    {
        internal AbiCmd cmd;
        public AbiCmdDecorator(AbiCmd cmd) : base (cmd.tool, cmd.context)
        {
            this.cmd = cmd;
        }
    }
    #endregion

    //concrete factory, makes only commands related to signfile
    public class AbiSignFileCommandFactory : AbiToolCommandFactory
    {
        public AbiSignFileCommandFactory(AbiTool tool, ContextObject context) : base(tool, context) { }

        public override AbiCmd CreateCommand(string cmd)
        {
            AbiCmd abiCmd = null;
            switch (cmd)
            {
                case "sign":
                    abiCmd = new SignCommand(tool, context);
                    switch(this.tool.version)
                    {
                        //if the SignFile sign command changes, use this to modify the behavior but dont mess with the original impl
                        case "2.0":
                            abiCmd = new SignCommand_v2(abiCmd);
                            break;
                    }
                    break;
                default:
                    throw new Exception("Unsupported Signing Command");

            }
            return abiCmd;
        }
    }

    //Sign "is a" abi command and it "has a" AbiTool it uses to sign
    public class SignCommand : AbiCmd
    {
        internal List<Tuple<string, string>> tuples;

        public SignCommand(AbiTool tool, ContextObject context) :base(tool, context)
        {
            tuples = new List<Tuple<string, string>>();

        }

        public override AbiCmdResult DoWork(string target)
        {
            SignContext context = (SignContext)this.context.GetTarget(target);
            this.tuples.Add(context.hash_algo);
            this.tuples.Add(context.cert);
            return this.tool.DoWork(this);
        }

        public override List<Tuple<string, string>> GetArgTuples()
        {
            return this.tuples;
        }

        public override string GetArgsString()
        {
            string args = "";
            //read the tuples, make a arg cmd line string
            foreach(Tuple<string, string> t in tuples)
            {
                args += string.Format("{0} {1} ", t.Item1, t.Item2);
            }
            return args;
        }
    }

    //new version of tool interface for the sign action
    public class SignCommand_v2 : AbiCmdDecorator
    {
        List<Tuple<string, string>> tuples;

        public SignCommand_v2(AbiCmd abiCmd)
            : base(abiCmd)
        {
            tuples = new List<Tuple<string, string>>();
            //some jask ass changed an arg pattern
            Tuple<string, string> t = new Tuple<string, string>("--hash_algo", (this.cmd.context as SignContext).hash_algo.Item2);
            this.tuples.Add(t);
            this.tuples.Add((this.cmd.context as SignContext).cert);
            
            //overload the base command tuples with new tuples
            (this.cmd as SignCommand).tuples = this.tuples;
        }

        public override AbiCmdResult DoWork(string target)
        {
            return this.cmd.DoWork(target);
        }

        public override List<Tuple<string, string>> GetArgTuples()
        {
            return this.tuples;
        }

        public override string GetArgsString()
        {
            string args = "";
            //read the tuples, make a arg cmd line string
            foreach(Tuple<string, string> t in tuples)
            {
                args += string.Format("{0} {1} ", t.Item1, t.Item2);
            }
            return args;
        }
    }



    //impl these
    public abstract class AbiCmdResult
    {
        public int code;
        public string stdout;
        public ContextObject context;

        internal RunTimeContext getContext()
        {
            throw new NotImplementedException();
        }

        public AbiCmdResult(Process p)
        {
            this.code = p.ExitCode;
            this.stdout = p.StandardOutput.ReadToEnd();
        }
    }

    public class SignCmdResult : AbiCmdResult
    {
        public SignCmdResult(Process p, ContextObject in_context) : base(p) 
        {
            //in_context is the context that was used tobuild the command, this would parse the stdout and add into to the context results but specific to this commands
            //read stdout/err, update in_context with result info
            this.context = in_context;
        }
    }
}
