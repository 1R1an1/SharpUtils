/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SharpUtils.Utilities
{
	public static class NetworkUtils
	{
		public static IPAddress GetLocalIPv4()
		{
			foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (ni.OperationalStatus != OperationalStatus.Up)
					continue;


				string desc = ni.Description.ToLower();
				string name = ni.Name.ToLower();

				// Ignorar VPN y adaptadores virtuales
				if (desc.Contains("vpn") || desc.Contains("virtual") || desc.Contains("loopback") || desc.Contains("tunnel")
				 || name.Contains("vpn") || name.Contains("virtual") || name.Contains("loopback") || name.Contains("tunnel"))
					continue;

				var props = ni.GetIPProperties();

				// Debe tener puerta de enlace (red real)
				if (props.GatewayAddresses.Count == 0)
					continue;

				foreach (var ip in props.UnicastAddresses)
				{
					if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						// Devolvemos la primera IP LAN real
						return ip.Address;
					}
				}
			}

			// fallback a loopback si no encuentra nada
			return IPAddress.Loopback;
		}
	}
}
