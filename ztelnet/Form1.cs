using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace ztelnet
{
	// NOTE:
	//
	// No class should appear in the file before Form1!
	//
	// See Q318603 for the incredibly stupid reason why!

	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class Form1 : System.Windows.Forms.Form
	{
		private bool EnableDebugOutput = false;


		private System.Windows.Forms.TextBox Command;
		private System.Windows.Forms.Button Send;
		private System.Windows.Forms.TextBox Log;
		private System.Windows.Forms.CheckBox LocalEcho;
		private System.Windows.Forms.CheckBox AutoCRLF;
		private System.Windows.Forms.TextBox Host;
		private System.Windows.Forms.TextBox Port;
		private System.Windows.Forms.Button Connect;
		private System.Windows.Forms.Button Disconnect;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private System.Windows.Forms.ComboBox KnownCommands;
		private System.Windows.Forms.ComboBox KnownParameters;
		private System.Windows.Forms.CheckBox ExitWhenDisconnected;

		private bool GoingDown = false;
		private bool ExpandEnvironmentStringsInScript;
		private string CrlfReplaceInScript;
		private string EmptyReplaceInScript;
		private string ScriptName;
		private int InterActionDelay_ms;

		protected override void OnClosed( EventArgs e  )
		{
			GoingDown = true;
			ActionDisconnect();
		} // OnClosed

		private bool ParseScriptArg( string arg )
		{
			if ( arg.IndexOf( "-script:" ) == 0 )
			{
				ScriptName = arg.Substring( 8 );
				scriptReader = new StreamReader( ScriptName );
				return true;
			}
			return false;
		}

		private bool ParsePermaLogArg( string arg )
		{
			if ( arg.IndexOf( "-log:" ) == 0 )
			{
				string log = arg.Substring( 5 );
				PermaLog = new StreamWriter( log, true );
				return true;
			}
			return false;
		}

		private bool GetBoolState( string state )
		{
			char first = state[0];
			switch ( first )
			{
				case 'f': return false;
				case 'F': return false;
				case '0': return false;
				case 'n': return false;
				case 'N': return false;
				case 't': return true;
				case 'T': return true;
				case '1': return true;
				case 'y': return true;
				case 'Y': return true;
			}
			throw new Exception( "Cannot parse boolean state " + state );
		} // GetBoolState

		private int GetIntState( string state )
		{
			return int.Parse( state );
		} // GetIntState

		private bool GetStateFromEnv( string envVarName, bool defaultState )
		{
			string envVar = System.Environment.GetEnvironmentVariable( envVarName );
			if ( envVar == null )
				return defaultState;
			if ( envVar == "" ) // This is false
				return false;
			try
			{
				return GetBoolState( envVar );
			}
			catch ( Exception )
			{
				// We don't care if GetBoolState failed
				if ( EnableDebugOutput )
					System.Diagnostics.Debug.WriteLine( "Environment var " + envVarName + " cannot be converted to bool. Assuming default value " + defaultState );
			}
			return defaultState;
		} // GetStateFromEnv

		private int GetStateFromEnv( string envVarName, int defaultState )
		{
			string envVar = System.Environment.GetEnvironmentVariable( envVarName );
			if ( envVar == null )
				return defaultState;
			if ( envVar == "" ) // This is 0
				return 0;
			try
			{
				return GetIntState( envVar );
			}
			catch ( Exception )
			{
				// We don't care if GetIntState failed
				if ( EnableDebugOutput )
					System.Diagnostics.Debug.WriteLine( "Environment var " + envVarName + " cannot be converted to int. Assuming default value " + defaultState );
			}
			return defaultState;
		} // GetStateFromEnv

		private string GetStateFromEnv( string envVarName, string defaultState )
		{
			string envVar = System.Environment.GetEnvironmentVariable( envVarName );
			if ( envVar == null )
				return defaultState;
			return envVar;
		} // GetStateFromEnv

		protected string ExpandEnvironmentStrings( string what )
		{
			string res = System.Environment.ExpandEnvironmentVariables( what );

			// Now expand all the pseudo variables
			if ( ScriptName != null && ScriptName != "" )
				res = res.Replace( "%ZTELNET_SCRIPT_NAME%", ScriptName );
			return res;
		} // ExpandEnvironmentStrings

		protected override void OnLoad( EventArgs e  )
		{
			//Toolbox.HMAC_MD5.test( "message digest" );
			//byte[] digest = Toolbox.HMAC_MD5.hmac_md5( "what do ya want for nothing?", "Jefe" );
			//byte[] key = new Byte[16];
			//for ( int i = 0; i < 16; i++ ) key[i] = 0xAA;
			//byte[] data = new byte[50];
			//for ( int i = 0; i < 50; i++ ) data[i] = 0xDD;

			// byte[] digest = Toolbox.HMAC_MD5.hmac_md5( data, key );
			//string respond = Toolbox.HMAC_MD5.CreateResponseStringToAuthLogin( "1234", "ranis", "a0" );


			bool autoConnect = false;
			base.OnLoad( e );

			// Set up the action registry, used to invoke actions dynamically
			actionRegistry.Register( "quit", new Action( ActionQuit ), @"\s*" );
			actionRegistry.Register( "connect", new Action( ActionConnect ), @"\s*(?<1>\S+)\s*(?<2>\S+)" );
			actionRegistry.Register( "disconnect", new Action( ActionDisconnect ), null );
			actionRegistry.Register( "send", new Action( ActionSend ), @"\s*(?<1>.*)" );
			actionRegistry.Register( "receive", new Action( ActionReceive ), @"\s*(?<1>.*)" );
			actionRegistry.Register( "setoption", new Action( ActionSetOption ), @"\s*(?<1>\w+)\s*=\s*(?<2>.*)\s*" );
			actionRegistry.Register( "delay", new Action( ActionDelay ), @"\s*(?<1>\d+)" );
			actionRegistry.Register( "authlogin", new Action( ActionAuthLogin ), @"\s*(?<1>\w+)\s+(?<2>\w+)" );
			actionRegistry.Register( "validate", new Action( ActionValidate ), @"\s*(?<1>\w+)\s+(?<2>.*)" );
			actionRegistry.Register( "echo", new Action( ActionEcho ), @"\s*(?<1>.*)" );
			actionRegistry.Register( "sendfile", new Action( ActionSendFile ), @"\s*(?<1>.*)" );

			Host.Text = "";
			Port.Text = "";

			// Load state from environment variables
			ExitWhenDisconnected.Checked = GetStateFromEnv( "ZTELNET_EXIT_WHEN_DISCONNECTED", false );
			AutoCRLF.Checked = GetStateFromEnv( "ZTELNET_AUTO_CRLF", true );
			ExpandEnvironmentStringsInScript = GetStateFromEnv( "ZTELNET_EXPAND_ENVIRONMENT_STRINGS_IN_SCRIPT", false );
			CrlfReplaceInScript = GetStateFromEnv( "ZTELNET_CRLF_REPLACE_IN_SCRIPT", null );
			EmptyReplaceInScript = GetStateFromEnv( "ZTELNET_EMPTY_REPLACE_IN_SCRIPT", null );
			InterActionDelay_ms = GetStateFromEnv( "ZTELNET_INTER_ACTION_DELAY", 0 );

			// Command-line arguments override environment variables
			string[] args = System.Environment.GetCommandLineArgs();
			if ( args.Length > 1 )
			{
				for ( int i = 1; i < args.Length; i++ )
				{
					string arg = args[i];
					if ( ParseScriptArg( arg ) )
						continue;
					if ( ParsePermaLogArg( arg ) )
						continue;
					// Else, can only be a host or a port number
					if ( Host.Text == "" )
						Host.Text = arg;
					else if ( Port.Text == "" )
						Port.Text = arg;
					else
						throw new ArgumentException( "Don't know what to do with argument " 
							+ i + "(" + arg + ")" );
				}

				if ( Port.Text != "" )
					autoConnect = true;

				if ( autoConnect )
					ActionConnect( Host.Text, Port.Text );

				ActionContinueScript();
			}

		} // OnLoad

		private ZCommands   commands;
		private ZParameters parameters;

		public Form1()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			commands = new ZCommands( 
				new ZCommand[]
				{
					new ZCommand( "", false, null ),
					new ZCommand( "HELO ", true, null ),
					new ZCommand( "MAIL FROM:", true, null ),
					new ZCommand( "RCPT TO:", true, null ),
					new ZCommand( "DATA", false, null ),
					new ZCommand( "To:", true, null ),
					new ZCommand( "From:", true, null ),
					new ZCommand( "Subject:", true, null ),
					new ZCommand( "", false, null ),
					new ZCommand( "QUIT", false, null ),

			} );

			commands.AddRange( KnownCommands.Items );

			parameters = new ZParameters( 
				new ZParameter[]
				{
					new ZParameter( "zivc-1.middleeast.corp.microsoft.com" ),
					new ZParameter( "<ziv@zivc-1.middleeast.corp.microsoft.com>" ),
					new ZParameter( "<zivc@microsoft.com>" ),
					new ZParameter( "<ziv@zivc-1.com>" ),
			} );

			parameters.AddRange( KnownParameters.Items );

			SetConnectionState( false );
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}

				if ( PermaLog != null )
				{
					PermaLog.Close();
					PermaLog = null;
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(Form1));
			this.Command = new System.Windows.Forms.TextBox();
			this.Send = new System.Windows.Forms.Button();
			this.Log = new System.Windows.Forms.TextBox();
			this.LocalEcho = new System.Windows.Forms.CheckBox();
			this.AutoCRLF = new System.Windows.Forms.CheckBox();
			this.Host = new System.Windows.Forms.TextBox();
			this.Port = new System.Windows.Forms.TextBox();
			this.Connect = new System.Windows.Forms.Button();
			this.Disconnect = new System.Windows.Forms.Button();
			this.KnownCommands = new System.Windows.Forms.ComboBox();
			this.KnownParameters = new System.Windows.Forms.ComboBox();
			this.ExitWhenDisconnected = new System.Windows.Forms.CheckBox();
			this.SuspendLayout();
			// 
			// Command
			// 
			this.Command.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.Command.Location = new System.Drawing.Point(8, 40);
			this.Command.Name = "Command";
			this.Command.Size = new System.Drawing.Size(624, 20);
			this.Command.TabIndex = 5;
			this.Command.Text = "";
			// 
			// Send
			// 
			this.Send.Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right);
			this.Send.Location = new System.Drawing.Point(640, 40);
			this.Send.Name = "Send";
			this.Send.TabIndex = 6;
			this.Send.Text = "&Send";
			this.Send.Click += new System.EventHandler(this.Send_Click);
			// 
			// Log
			// 
			this.Log.Anchor = (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
				| System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.Log.Location = new System.Drawing.Point(8, 104);
			this.Log.Multiline = true;
			this.Log.Name = "Log";
			this.Log.ReadOnly = true;
			this.Log.Size = new System.Drawing.Size(720, 144);
			this.Log.TabIndex = 2;
			this.Log.Text = "";
			// 
			// LocalEcho
			// 
			this.LocalEcho.Anchor = (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left);
			this.LocalEcho.Checked = true;
			this.LocalEcho.CheckState = System.Windows.Forms.CheckState.Checked;
			this.LocalEcho.Location = new System.Drawing.Point(8, 256);
			this.LocalEcho.Name = "LocalEcho";
			this.LocalEcho.Size = new System.Drawing.Size(88, 24);
			this.LocalEcho.TabIndex = 7;
			this.LocalEcho.Text = "Local &echo";
			// 
			// AutoCRLF
			// 
			this.AutoCRLF.Anchor = (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left);
			this.AutoCRLF.Checked = true;
			this.AutoCRLF.CheckState = System.Windows.Forms.CheckState.Checked;
			this.AutoCRLF.Location = new System.Drawing.Point(88, 256);
			this.AutoCRLF.Name = "AutoCRLF";
			this.AutoCRLF.Size = new System.Drawing.Size(128, 24);
			this.AutoCRLF.TabIndex = 8;
			this.AutoCRLF.Text = "&Automatic CRLF";
			// 
			// Host
			// 
			this.Host.Location = new System.Drawing.Point(8, 8);
			this.Host.Name = "Host";
			this.Host.Size = new System.Drawing.Size(152, 20);
			this.Host.TabIndex = 1;
			this.Host.Text = "Host";
			// 
			// Port
			// 
			this.Port.Location = new System.Drawing.Point(168, 8);
			this.Port.Name = "Port";
			this.Port.TabIndex = 2;
			this.Port.Text = "Port";
			// 
			// Connect
			// 
			this.Connect.Location = new System.Drawing.Point(272, 8);
			this.Connect.Name = "Connect";
			this.Connect.TabIndex = 3;
			this.Connect.Text = "&Connect";
			this.Connect.Click += new System.EventHandler(this.Connect_Click);
			// 
			// Disconnect
			// 
			this.Disconnect.Location = new System.Drawing.Point(352, 8);
			this.Disconnect.Name = "Disconnect";
			this.Disconnect.TabIndex = 4;
			this.Disconnect.Text = "&Disconnect";
			this.Disconnect.Click += new System.EventHandler(this.Disconnect_Click);
			// 
			// KnownCommands
			// 
			this.KnownCommands.AllowDrop = true;
			this.KnownCommands.Cursor = System.Windows.Forms.Cursors.Default;
			this.KnownCommands.Location = new System.Drawing.Point(8, 72);
			this.KnownCommands.MaxDropDownItems = 24;
			this.KnownCommands.Name = "KnownCommands";
			this.KnownCommands.Size = new System.Drawing.Size(232, 21);
			this.KnownCommands.TabIndex = 9;
			this.KnownCommands.SelectedIndexChanged += new System.EventHandler(this.KnownCommands_SelectedIndexChanged);
			// 
			// KnownParameters
			// 
			this.KnownParameters.AllowDrop = true;
			this.KnownParameters.Cursor = System.Windows.Forms.Cursors.Default;
			this.KnownParameters.Location = new System.Drawing.Point(248, 72);
			this.KnownParameters.MaxDropDownItems = 24;
			this.KnownParameters.Name = "KnownParameters";
			this.KnownParameters.Size = new System.Drawing.Size(384, 21);
			this.KnownParameters.TabIndex = 10;
			this.KnownParameters.SelectedIndexChanged += new System.EventHandler(this.KnownParameters_SelectedIndexChanged);
			// 
			// ExitWhenDisconnected
			// 
			this.ExitWhenDisconnected.Anchor = (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left);
			this.ExitWhenDisconnected.Location = new System.Drawing.Point(192, 256);
			this.ExitWhenDisconnected.Name = "ExitWhenDisconnected";
			this.ExitWhenDisconnected.Size = new System.Drawing.Size(160, 24);
			this.ExitWhenDisconnected.TabIndex = 11;
			this.ExitWhenDisconnected.Text = "E&xit when disconnected";
			// 
			// Form1
			// 
			this.AcceptButton = this.Send;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(736, 286);
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.ExitWhenDisconnected,
																		  this.KnownParameters,
																		  this.KnownCommands,
																		  this.Disconnect,
																		  this.Connect,
																		  this.Port,
																		  this.Host,
																		  this.AutoCRLF,
																		  this.LocalEcho,
																		  this.Log,
																		  this.Send,
																		  this.Command});
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "Form1";
			this.Text = "Form1";
			this.ResumeLayout(false);

		}
		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new Form1());
		}

		private void Send_Click(object sender, System.EventArgs e)
		{
			// Get the command
			string cmd = this.Command.Text;

			// If there's no command, try to see if a known
			// command was selected, and use that instead
			if ( cmd == null || cmd == "" )
			{
				string knownCmd = KnownCommands.Text;
				if ( knownCmd != null && knownCmd != "" )
				{
					// Does the command require a parameter?
					if ( KnownParameters.Enabled )
						cmd = knownCmd + KnownParameters.Text;
					else
						cmd = knownCmd;
				}
			}

			ActionSend( cmd );
		} // Send_Click

		#region Logger support
		private TextWriter  PermaLog;

		[Flags]
		enum LogTargets
		{
			GuiLog   = 0x01,
			PermaLog = 0x02,
			All      = 0x03
		} // enum LogTargets

		private void AppendToLog( string what, LogTargets where )
		{
			if ( (where & LogTargets.GuiLog) != 0 )
				if ( Log != null )
                    Log.AppendText( what );
			if ( (where & LogTargets.PermaLog) != 0 )
				if ( PermaLog != null )
					PermaLog.Write( what );
			if ( EnableDebugOutput )
				System.Diagnostics.Debug.WriteLine( what );
		} // AppendToLog
		#endregion

		#region Private connection data
		private TelnetChannel Channel;
		#endregion

		#region The actual actions that we support
		private TextReader scriptReader;

		ActionRegistry actionRegistry = new ActionRegistry();

		private bool ActionQuit( params string[] args )
		{
			if ( args.Length != 0 )
				throw new Exception( "ActionQuit takes no parameters." );
			this.Close();
			Application.Exit();
			return false; // Dummy
		} // ActionQuit

		private bool ActionConnect( params string[] args )
		{
			if ( args.Length != 2 )
				throw new Exception( "ActionConnect requires two parameters, host and port."
					+ " Given " + args.Length + " instead." );
			this.Host.Text = args[0];
			this.Port.Text = args[1];
			
			if ( Channel != null )
			{
				ActionDisconnect();
			}

			try 
			{
				Channel = new TelnetChannel( new TelnetChannel.OnResponse( OnResponse ),
					new TelnetChannel.OnConnectionClosed( OnConnectionClosed ),
					Host.Text, Port.Text );
				SetConnectionState( true );
				AppendToLog( "########## Connected: " + Host.Text + ":" + Port.Text + "\r\n", LogTargets.All );
			}
			catch ( Exception e )
			{
				SetConnectionState( false );
				AppendToLog( "########## Connect to " + Host.Text + ":" + Port.Text + " failed: " + e.ToString() + "\r\n", LogTargets.All );
			}

			return true;
		} // ActionConnect

		private bool ActionDisconnect( params string[] args )
		{
			if ( args.Length != 0 )
				throw new Exception( "ActionDisconnect needs no parameters."
					+ " Given " + args.Length + " instead." );


			// If we're waiting for some text, give it up -- it's not going to get here
			IncomingRegex = null;

			if ( Channel != null )
				Channel.Disconnect();
			Channel = null;
			SetConnectionState( false );
			if ( ExitWhenDisconnected.Checked )
			{
				if ( !GoingDown )
				{
					ActionQuit();
				}
				else
				{
					System.Diagnostics.Debug.WriteLine( "ActionDisconnect: ActionQuit already called, so it is not being called again" );
				}
			}

			return true;
		} // ActionDisconnect

		private string CookArgs( string command, params string[] args )
		{
			if ( args.Length == 0 )
				throw new Exception( command + " requires at least one string to send." );

			// Create a string that concats all the arguments together
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach( string s in args )
				sb.Append( s );

			// If automatic CRLF, add CRLF
			if ( this.AutoCRLF.Checked )
			{
				sb.Append( "\r\n" );
			}

			// If character transflation is turned on, do it now
			// TODO: Decide if we're not interested in also adding headers to the
			//       permalog after each CRLF
			if ( CrlfReplaceInScript != null && CrlfReplaceInScript != "" )
				sb = sb.Replace( CrlfReplaceInScript, "\r\n" );
			
			if ( EmptyReplaceInScript != null && EmptyReplaceInScript != "" )
				sb = sb.Replace( EmptyReplaceInScript, "" );

			string str = sb.ToString();
			return str;

		} // CookArgs

		private bool ActionSend( params string[] args )
		{
			string cmd = CookArgs( "ActionSend", args );

			// If local echo, echo locally
			if ( this.LocalEcho.Checked )
			{
				AppendToLog( cmd, LogTargets.GuiLog );
			}
			//if ( cmd.StartsWith( "Subject" ) )
			//	System.Diagnostics.Debugger.Break();
			
			// Write for posterity
			AppendToLog( "S: " + cmd, LogTargets.PermaLog );

			// Send it over to the other side
			if ( Channel != null )
				Channel.Send( cmd );
			else
				AppendToLog( "########## Send failed: no connection\r\n", LogTargets.All );

			// Clear the current line in preparation to the next
			this.Command.Text = "";

			return true;
		} // ActionSend

		private bool ActionSendFile( params string[] args )
		{
			if ( args.Length != 1 )
				throw new Exception( "ActionSendFile expects one argument, the filename to send."
					+ " Given " + args.Length + " arguments instead." );

			bool fLocalEcho = this.LocalEcho.Checked;
			
			if ( fLocalEcho )
				AppendToLog( ">>> Sending file " + args[0] + " to the other side", LogTargets.GuiLog );

			// Pipe the file through the channel
			FileStream file = new FileStream( args[0], FileMode.Open );
			byte[] buff = new Byte[1024];
			while ( true )
			{
				int bytesRead = file.Read( buff, 0, buff.Length );

				// If local echo, echo locally
				string buffAsStr = System.Text.Encoding.UTF8.GetString( buff, 0, bytesRead );
				if ( fLocalEcho )
				{
					AppendToLog( buffAsStr, LogTargets.GuiLog );
				}
				AppendToLog( "S: " + buffAsStr, LogTargets.PermaLog );

				// Send it over to the other side
				if ( Channel != null )
					Channel.Send( buff, 0, bytesRead );
				else
				{
					AppendToLog( "########## Send failed: no connection\r\n", LogTargets.All );
					break;
				}

				if ( bytesRead < buff.Length )
					break;
			}

			if ( fLocalEcho )
				AppendToLog( ">>> Completed sending file", LogTargets.GuiLog );

			// Clear the current line in preparation to the next
			this.Command.Text = "";

			return true;
		}

		private bool ActionEcho( params string[] args )
		{
			string cmd = CookArgs( "ActionEcho", args );

			AppendToLog( cmd, LogTargets.All );

			return true;
		} // ActionEcho

		private bool ActionReceive( params string[] args )
		{
			// Receive action, unlike the others, is passive.
			// Rather than do something, we wait for some event to occur.
			// When that event occurs, we continue running the script.
			// When we're told to do a receive action, what we do is we set up
			// the receive handler to start comparing all the incoming messages
			// to the specific pattern that we want to identify.
			// When that pattern is identified, we simply nudge the script
			// engine again.
			if ( args.Length != 1 )
				throw new Exception( "ActionReceive expects one argument, the regex to wait for."
					+ " Given " + args.Length + " arguments instead." );
			if ( Channel == null )
			{
				AppendToLog( "########## Receive ignored because there's no connection\r\n", LogTargets.All );
				return true;
			}

			// Massage the regex
			string str = args[0];
			if ( CrlfReplaceInScript != null && CrlfReplaceInScript != "" )
			{ // Replace occurrences of the string with a CRLF
				str = str.Replace( CrlfReplaceInScript, "\r\n" );
			}

			if ( AutoCRLF.Checked )
			{ // Append a \r\n to the regex if it doesn't already have one
				if ( str.LastIndexOf( "\r\n" ) < 0 )
					str += "\r\n";
			}

			if ( IncomingRegex != null )
				throw new Exception( "ActionReceive invoked, but a previous receive operation has not been completed." );

			IncomingRegex = new Regex( str );
			IncomingLastRegMatch = null;

			return ConsumeIncomingUnprocessedString();
		} // ActionReceive

		private bool ActionSetOption( params string[] args )
		{
			if ( args.Length != 2 )
				throw new Exception( "ActionSetOption expects two arguments, the option name and its value."
					+ " Given " + args.Length + " arguments instead." );
			string name = args[0];
			string valu = args[1];
			// TODO: Obviously we need somthing more regulat, and integrate
			//       it with the rest of the program
			switch ( name )
			{
					// ZTELNET_SCRIPT_NAME is not an option the user can set
					// TODO: We also need to change the env var value, currently we only affect the internal state
				case "ZTELNET_EXIT_WHEN_DISCONNECTED": ExitWhenDisconnected.Checked = GetBoolState( valu ); break;
				case "ZTELNET_EXPAND_ENVIRONMENT_STRINGS_IN_SCRIPT": ExpandEnvironmentStringsInScript = GetBoolState( valu ); break;
				case "ZTELNET_CRLF_REPLACE_IN_SCRIPT": CrlfReplaceInScript = valu; break;
				case "ZTELNET_EMPTY_REPLACE_IN_SCRIPT": EmptyReplaceInScript = valu; break;
				case "ZTELNET_INTER_ACTION_DELAY": InterActionDelay_ms = GetIntState( valu ); break;
				case "ZTELNET_AUTO_CRLF": AutoCRLF.Checked = GetBoolState( valu ); break;
			}
			return true;
		} // ActionSetOption

		private bool ActionDelay( params string[] args )
		{
			if ( args.Length != 1 )
			throw new Exception( "ActionDelay expects one argument, how long to delay in ms."
				+ " Given " + args.Length + " arguments instead." );
			int delay_ms = int.Parse( args[0] );

			System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
			timer.Tick += new EventHandler( ActionDelayHandler );
			timer.Interval = delay_ms;
			timer.Start();
			return false;
		} // ActionDelay

		private void ActionDelayHandler( Object sender, EventArgs evArgs ) 
		{
			System.Windows.Forms.Timer timer = (System.Windows.Forms.Timer)sender;
			timer.Stop();
			timer.Dispose();
			ActionContinueScript();
			
		} // ActionDelayHandler

		private bool ActionAuthLogin( params string[] args )
		{
			if ( args.Length != 2 )
				throw new Exception( "ActionAuthLogin expects two arguments, username and password."
					+ " Given " + args.Length + " arguments instead." );
			string username = args[0];
			string password = args[1];
			string challenge = "";
			Regex c = new Regex( @"\d\d\d\s+(?<challenge>.*)\r\n" );
			Match m = c.Match( IncomingLastRegMatch );
			if ( m.Success ) // TODO: What if not?
			{
				GroupCollection gc = m.Groups;
				challenge = gc["challenge"].Value;

				// TODO: Is there a possibility that we'll translate the auto CRLF character here?!
				string response = Toolbox.HMAC_MD5.CreateResponseStringToAuthLogin( challenge, username, password );

				if ( !this.AutoCRLF.Checked )
					response += "\r\n";

				ActionSend( response );
			}
			
			return true;
		} // ActionAuthLogin

		private bool ActionValidate( params string[] args )
		{
			if ( args.Length < 2 )
				throw new Exception( "ActionValidate expects two or more arguments."
					+ " Given " + args.Length + " arguments instead." );
			bool valid = false;
			string failureReason = "";
			string what = args[0];
			switch ( what ) 
			{
				case "receive":
					if ( IncomingLastRegMatch == null || IncomingLastRegMatch == "" )
					{
						failureReason = "[No active receive match was found]";
					}
					else
					{
						string pattern = args[1];
						Regex r = new Regex( pattern );
						Match m = r.Match( IncomingLastRegMatch );
						valid = m.Success;
						if ( !valid )
						{
							failureReason = "[Last receive match: [" + IncomingLastRegMatch + "]; Validation regex: [" + pattern + "]";
						}
					}
					break;

				case "connection":
					string reqState = args[1];
					string conState = ((Channel != null && Channel.Connected) ? "up" : "down");
					valid = (reqState == conState);
					break;
				default:
					throw new Exception( "ActionValidate currently supports only 'receive' or 'connection' verbs."
						+ " Given " + args[0] + " instead." );
			}
			if ( valid )
			{
				AppendToLog( ">> OK (" + ScriptName + ")\r\n", LogTargets.All );
			}
			else
			{
				AppendToLog( ">> FAILURE (" + ScriptName + ")" + failureReason + "\r\n", LogTargets.All );
			}

			return true;
		} // ActionValidate

		private bool ActionContinueScript( params string[] args )
		{
			if ( args.Length != 0 )
				throw new Exception( "ActionContinuteScript needs no arguments." );
			if ( scriptReader != null )
			{
				bool continueRunningScript = true;
				while ( continueRunningScript )
				{
					// Get the next line
					string line = scriptReader.ReadLine();

					bool traceScriptExecution = true;
					// End of script
					if ( line == null )
					{
						if ( traceScriptExecution )
                            System.Diagnostics.Debug.WriteLine( "ActionContinueScript: Script end reached." );
						break;
					}
					else
					{
						if ( traceScriptExecution )
							System.Diagnostics.Debug.Write( "ActionContinueScript: Executing line: " );
							System.Diagnostics.Debug.WriteLine( line );
					}

					// Expand environment vars, etc.
					if ( ExpandEnvironmentStringsInScript )
					{
						line = ExpandEnvironmentStrings( line );
					}

					// Delay if we are told to delay
					if ( InterActionDelay_ms != 0 )
						System.Threading.Thread.Sleep( InterActionDelay_ms );

					// Convert into an action and invoke using the action registry
					continueRunningScript = actionRegistry.Invoke( line );
				}
			}

			return false;
		} // ActionContinueScript
		#endregion

		#region
		#endregion

		private void Connect_Click(object sender, System.EventArgs e)
		{
			ActionConnect( Host.Text, Port.Text );
		}

		private void SetConnectionState( bool connected )
		{
			Host.Enabled       = !connected;
			Port.Enabled       = !connected;
			Connect.Enabled    = !connected;
			Disconnect.Enabled = connected;
			Command.Enabled    = connected;
			Send.Enabled       = connected;
			if ( connected )
			{
				Command.Select();
				AcceptButton = Send;
				Text = Host.Text + ":" + Port.Text + " (ZTelnet)";
				KnownCommands.Enabled = true;
			}
			else
			{
				Host.Select();
				AcceptButton = Connect;
				Text = "ZTelnet (disconnected)";
				KnownCommands.Enabled = false;
			}
			SetKnownParametersState();
		} // SetConnectionState

		#region Incoming event handlers
		private delegate void pIncoming( string text );

		private void OnResponse( string response )
		{
			pIncoming action = new pIncoming( Incoming );
			object[] args = new object[1]{ response };
			this.Invoke( action, args );
		} // OnResponse

		// Handling incoming data from the other side
		private Regex IncomingRegex;				// The regex provided by the last receive.
													// Until this regex is matched, the script will
													// not progress. (TODO: Unless the connection is closed?)

		private string IncomingUnprocessedString;	// All the incoming information that has not
													// yet been matched by an IncomingRegex.
		
		private string IncomingLastRegMatch;		// The last echo part of the IncomingRegex.


		private void Incoming( string text )
		{
			// Append the information just received into the unprocessed buffer
			// (perhaps partial data), we take note of it
			if ( IncomingUnprocessedString != null )
				IncomingUnprocessedString += text;
			else
				IncomingUnprocessedString = text;

// TODO: What to do here?????
//			// If the received text ends with a \r\n,
//			// but we don't, add \r\n as a curtesy
//			string echo = gc["echo"].Value;
//			if ( echo.LastIndexOf( "\r\n" ) < 0 &&
//				text.LastIndexOf( "\r\n" ) >= 0 )
//				text = echo + "\r\n";
//			else
//				text = echo;

			// Add text to GUI log (if it exists)
			AppendToLog( text, LogTargets.GuiLog );

			// Add text to CUI log (if it exists)
			AppendToLog( "R: " + text, LogTargets.PermaLog );

			bool cont = ConsumeIncomingUnprocessedString();
			
			// Kick the script (if it exists)
			if ( cont ) 
                ActionContinueScript();
		} // Incoming

		private bool ConsumeIncomingUnprocessedString() 
		{
			bool doActionContinueScript = false;

			if ( IncomingRegex == null )
			{
				// Nobody is currently signed-up for the unprocessed string
				// yet. Continue running the script
				doActionContinueScript = true;
			}
			else if ( IncomingUnprocessedString != null )
			{
				// Try to match the unprocessed string with the regex
				Match m = IncomingRegex.Match( IncomingUnprocessedString );
				if ( m.Success )
				{
					// This regex has served its purpose
					IncomingRegex = null;

					doActionContinueScript = true;
					GroupCollection gc = m.Groups;

					// Save the part that matched for future reference (useful
					// for AUTH challenges, or validation)
					IncomingLastRegMatch = IncomingUnprocessedString.Substring( m.Index, m.Length );

					// If the match leaves unprocessed data,
					// save it for the next round
					int last = m.Index + m.Length;
					if ( IncomingUnprocessedString.Length > last )
						IncomingUnprocessedString = IncomingUnprocessedString.Substring( last );
					else
						IncomingUnprocessedString = null;
				}
			}

			return doActionContinueScript;
		} // ConsumeIncomingUnprocessedString

		private void OnConnectionClosed( string comment )
		{
			// TODO:
			//if ( GoingDown )
			//	return;

			OnConnectionClosedByPeer action = new OnConnectionClosedByPeer( ConnectionClosedByPeer );
			object[] args = new object[1]{ comment };
			this.Invoke( action, args );
		}

		private delegate void OnConnectionClosedByPeer( string comment );

		private void ConnectionClosedByPeer( string comment )
		{
			AppendToLog( "########## Disconnected: " + comment + "\r\n", LogTargets.All );
			ActionDisconnect();

			// Kick the script (if it exists)
			ActionContinueScript();
		}
		#endregion

		private void Disconnect_Click(object sender, System.EventArgs e)
		{
			ActionDisconnect();
		}

		private void KnownParameters_SelectedIndexChanged(object sender, System.EventArgs e)
		{
		
		}

		private void KnownCommands_SelectedIndexChanged(object sender, System.EventArgs e)
		{
			SetKnownParametersState();

		
		}
		private void SetKnownParametersState()
		{
			// KnownParameters is enabled or disabled according
			// to whether the current KnownCommand needs a parameter or not.
			
			if ( KnownCommands.Enabled == false )
				KnownParameters.Enabled = false;
			else
			{
				string knownCmd = KnownCommands.Text;
				if ( knownCmd != null && knownCmd != "" )
				{
					int index = KnownCommands.Items.IndexOf( knownCmd );
					if ( commands[index].AllowParams )
						KnownParameters.Enabled = true;
					else
						KnownParameters.Enabled = false;

					/*
					if ( knownCmd[knownCmd.Length-1] == ':' )
						KnownParameters.Enabled = true;
					else
						KnownParameters.Enabled = false;
					*/
				}
				else
					KnownParameters.Enabled = false;
			}
			
		}

	} // class Form1

	public class ZCommand
	{
		private string       name;
		private bool         allowParams;
		private ZParameters  parameters;

		public string Name { get { return name; } }
		public bool   AllowParams { get { return allowParams; } }
		public ZParameters Parameters { get { return parameters; } }

		public ZCommand( string name, bool allowParams, ZParameters parameters )
		{
			this.name        = name;
			this.allowParams = allowParams;
			this.parameters  = parameters;
		} // ctor

	} // class ZCommand

	public class ZCommands
	{
		private ZCommand[] commands;

		public ZCommands( ZCommand[] commands )
		{
			this.commands = commands;
		}

		public void AddRange( ComboBox.ObjectCollection collection  )
		{
			int howMany = commands.Length;
			object[] commandsAsObjects = new Object[ howMany ];
			for ( int i = 0; i < howMany; i++ )
			{
				commandsAsObjects[i] = commands[i].Name;
			}
			collection.AddRange( commandsAsObjects );
		}

		public ZCommand this[int index]
		{
			get
			{
				return commands[index];
			}
		}

	} // class ZCommands

	public class ZParameter
	{
		private string name;

		public string Name { get { return name; } }
		
		public ZParameter( string name )
		{
			this.name = name;
		}

	} // class ZParameter

	public class ZParameters
	{
		private ZParameter[] parameters;

		public ZParameters( ZParameter[] parameters )
		{
			this.parameters = parameters;
		}

		public void AddRange( ComboBox.ObjectCollection collection  )
		{
			int howMany = parameters.Length;
			object[] parametersAsObjects = new Object[ howMany ];
			for ( int i = 0; i < howMany; i++ )
			{
				parametersAsObjects[i] = parameters[i].Name;
			}
			collection.AddRange( parametersAsObjects );
		}

	} // class ZParameters

}
