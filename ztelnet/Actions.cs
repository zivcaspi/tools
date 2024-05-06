using System.Text.RegularExpressions;

namespace ztelnet
{

	public delegate bool Action( params string[] p );

	public class ActionRegistry
	{
		private bool DebugTraceActionInvoke = false;

		private struct ActionParams
		{
			public Action action;
			public Regex  paramsRegex;
		} // struct ActionParams

		public ActionRegistry()
		{
			actionRegistry = new System.Collections.Hashtable();
		} // ctor

		public void Register( string name, Action action, string paramsRegex )
		{
			ActionParams actionParams = new ActionParams();
			actionParams.action       = action;
			if ( paramsRegex != null && paramsRegex != "" )
				actionParams.paramsRegex = new Regex( paramsRegex, RegexOptions.Compiled );
			else
				actionParams.paramsRegex = null;

			lock ( this )
			{
				actionRegistry.Add( name, actionParams );
			}
		} // Register

		public bool Invoke( string line )
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder( line.Length +16 );

			// If this is a blank line, ignore the line
			if ( line == null || line == "" )
				return true;

			// If this is a comment line, ignore the line
			if ( CommentRegex.Match( line ).Success )
				return true;
			
			// Get the first word in the line. This is the action to perform.
			Match m = ActionRegex.Match( line );
			string actionName = m.Value;
			sb.AppendFormat( "Action=[{0}]", actionName );
			line = line.Substring( m.Index + m.Length );

			// Locate the action in the registry
			ActionParams actionParams;
			lock ( this )
			{
				actionParams = (ActionParams)actionRegistry[ actionName ];
			}

			// If the action requires parameters, extract them
			string[] args = null;
			Regex r = actionParams.paramsRegex;
			if ( r != null )
			{
				m = r.Match( line );
				GroupCollection gc = m.Groups;
				if ( gc.Count-1 != 0 ) // gc[0] is the entire match. We don't want it.
				{
					args = new string[gc.Count-1];
					for ( int i = 0; i < gc.Count-1; i++ )
					{
						args[i] = gc[i+1].Value;
						sb.AppendFormat( " Arg[{0}]=[{1}]", i, args[i] );
					}
				}
			}
			// Make sure we give an array, even an empty one.
			// The reason we do this is that because this function
			// might be called in the context of a control's Invoke method,
			// so the action *we* call will be under a try-catch which
			// will not be exposed, leading to very difficult bugs to locate.
			if ( args == null )
				args = new string[0];

			if ( DebugTraceActionInvoke )
				System.Diagnostics.Debug.WriteLine( "ActionRegistry.Invoke() about to execute: " + sb.ToString() );

			// Invoke
			Action action = actionParams.action;
			bool continueRunningScript = action( args );
			return continueRunningScript;
		} // Invoke

		private System.Collections.Hashtable actionRegistry;

		private Regex ActionRegex = new Regex( @"\s*(?<1>\w+)", RegexOptions.Compiled /*| RegexOptions.ExplicitCapture*/ );
		private Regex CommentRegex = new Regex( @"^\w*\x23", RegexOptions.Compiled );


	} // class ActionRegistry

} // namespace