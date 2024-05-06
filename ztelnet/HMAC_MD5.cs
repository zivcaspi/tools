

namespace Toolbox 
{
	using System.Security.Cryptography;

	class HMAC_MD5
	{
		/*
		static public void test( string s )
		{
			byte[] data = System.Text.Encoding.ASCII.GetBytes( s );
			MD5 md5 = new MD5CryptoServiceProvider();
			byte[] result = md5.ComputeHash( data );
			System.Diagnostics.Debug.Write( "MD5(" + s + ") = " );
			for ( int i = 0; i < result.Length; i++ )
				System.Diagnostics.Debug.Write( System.Convert.ToString( result[i], 16 ) );
			System.Diagnostics.Debug.WriteLine( "." );

		} // test
		*/

		static public byte[] Concat( byte[] a, byte[] b, byte[] c )
		{
			byte[] concat = new byte[ a.Length + b.Length + c.Length ];
			a.CopyTo( concat, 0 );
			b.CopyTo( concat, a.Length );
			c.CopyTo( concat, a.Length + b.Length );
			return concat;
		}

		static private byte[] Transform( MD5 md5, byte[] first, byte[] second )
		{
			// Concat both arrays
			byte[] concat = new byte[ first.Length + second.Length ];
			first.CopyTo( concat, 0 );
			second.CopyTo( concat, first.Length );

			//
			return md5.ComputeHash( concat );
		} // Transform

		static public byte[] hmac_md5(
			string data,
			string key )
		{
			byte[] data_ = System.Text.Encoding.ASCII.GetBytes( data );
			byte[] key_  = System.Text.Encoding.ASCII.GetBytes( key );
			return hmac_md5( data_, key_ );
		} // hmac_md5

		static public byte[] hmac_md5(
			byte[] data,
			byte[] key )
		{
			byte[] k_ipad = new byte[64];
			byte[] k_opad = new byte[64];
			MD5 md5;

			// If key is longer than 64 bytes, reset it to key=MD5(key)
			if ( key.Length > 64 ) 
			{
				md5 = new MD5CryptoServiceProvider();
				key = md5.ComputeHash( key );
			}

			// HMAC_MD5 looks like this:
			// MD5( k XOR opad, MD5(k XOR ipad, text))
			//
			// k    -- n-byte key
			// ipad -- 0x36 repeated 64 times
			// opad -- 0x5c repeated 64 times
			// text -- data being processed

			for ( int i = 0; i < k_ipad.Length; i++ ) k_ipad[i] = 0;
			for ( int i = 0; i < k_opad.Length; i++ ) k_opad[i] = 0;
			for ( int i = 0; i < key.Length; i++ )    k_ipad[i] = key[i];
			for ( int i = 0; i < key.Length; i++ )    k_opad[i] = key[i];

			// XOR key with ipad and opad values
			for ( int i = 0; i < 64; i++ )
			{
				k_ipad[i] ^= 0x36;
				k_opad[i] ^= 0x5c;
			}

			// Perform inner MD5
			byte[] digest = Transform( new MD5CryptoServiceProvider(), k_ipad, data );

			// Perform outer MD5
			digest = Transform( new MD5CryptoServiceProvider(), k_opad, digest );

			/*
			System.Diagnostics.Debug.Write( "= ..." );
			for ( int i = 0; i < digest.Length; i++ )
				System.Diagnostics.Debug.Write( System.Convert.ToString( digest[i], 16 ) );
			System.Diagnostics.Debug.WriteLine( "." );
			*/

			return digest;
		} // hmac_md5


		static public string CreateResponseStringToAuthLogin( string challenge, string username, string password )
		{
			byte[] digest = hmac_md5( password, challenge );
			byte[] username_ = System.Text.Encoding.ASCII.GetBytes( username );
			byte[] concat = Concat( username_, new byte[]{32}, digest );


			long arrayLength = (long) ((4.0d/3.0d) * concat.Length);
			if (arrayLength % 4 != 0) 
			{
				arrayLength += 4 - arrayLength % 4;
			}
         
			string response = System.Convert.ToBase64String( concat );
            return response;
		} // CreateResponseStringToAuthLogin

	} // class HMAC_MD5
} // namespace Toolbox