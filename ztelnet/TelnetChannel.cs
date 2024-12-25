using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace ztelnet
{
	public class TelnetChannel
	{
		// Delegates that we invoke on special events
		public delegate void OnResponse( string text );
		public delegate void OnConnectionClosed( string comment );

		// Private members
		private TcpClient		Connection;
		private Stream	        Stream;
		private Thread			ReceiveLoopThread;
		private OnResponse			RaiseResponse;
		private OnConnectionClosed	RaiseConnectionClosed;

		private bool connected = false;
		public bool Connected { get { return connected; } }

		private bool DebugTraceIncomingData = true;
		private bool DebugTraceOutgoingData = false;
		
		// Ctor
		public TelnetChannel( OnResponse response, OnConnectionClosed connectionClosed, string host, string port )
		{
			RaiseResponse			= response;
			RaiseConnectionClosed	= connectionClosed;

			var port_int = Int32.Parse( port );
			Connection = new TcpClient( host, port_int );
			Stream     = Connection.GetStream();

			if ( port_int == 443 )
			{
				Stream = new System.Net.Security.SslStream( Stream );
			}

			connected = true;

			// Start our receive thread....
			ReceiveLoopThread = new Thread( new ThreadStart( ReceiveLoop ) );
			ReceiveLoopThread.Start();
		}

		// Send path
		public void Send( string what )
		{
			if ( DebugTraceOutgoingData )
				System.Diagnostics.Debug.WriteLine( "<<<Sending<<<" + what );

			// Have a string, get a buffer
			System.Text.Encoding encoder = System.Text.Encoding.ASCII;
			byte[] whatAsBytes = encoder.GetBytes( what );
			Stream.Write( whatAsBytes, 0, whatAsBytes.Length );
		} // Send

		public void Send( byte[] buff, int offset, int size )
		{
			if ( DebugTraceOutgoingData )
				System.Diagnostics.Debug.WriteLine( "<<<Sending<<<" + " ...buffer (TODO)" );

			Stream.Write( buff, offset, size );
		}

		// Disconnect (by command) path
		public void Disconnect()
		{
			connected = false;
			Stream.Close();
		} // Disconnect

		// Receiver path
		// This requires a thread to do the work...
		private void ReceiveLoop()
		{
			try
			{
				while ( true )
				{
					// Get anything from the other side
					byte[] buffer = new Byte[1024];
					int n = Stream.Read( buffer, 0, buffer.Length );

					if ( n == 0 )
					{
						connected = false;
						RaiseConnectionClosed( "Peer gracefully shutdown" );
						break;
					}
			
					// Have a buffer, get a string
					System.Text.Encoding encoder = System.Text.Encoding.ASCII;
					string response = new string( encoder.GetChars( buffer, 0, n ) );
					if ( DebugTraceIncomingData )
						System.Diagnostics.Debug.WriteLine( ">>>Received>>>" + response );
					RaiseResponse( response );
				}

			}
			catch ( System.Exception )
			{
				connected = false;
				RaiseConnectionClosed( "Connection broke" );
			}
		} // ReceiveLoop

	} // class TelnetChannel

} // namespace ztelnet
