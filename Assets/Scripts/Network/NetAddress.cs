using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

// ============================================================
// NetAddress
//
// Las direcciones de la conexión, en un solo lugar:
//   · TryParse: lo que escribe el jugador ("192.168.0.5", "192.168.0.5:7770",
//     "algo.ply.gg:60625") partido en host y puerto. Sin puerto, el de siempre (7770).
//   · LanAddress: la IP de ESTA PC en la red local, la que el host les da a los demás para
//     que se conecten desde otras PCs de la misma red. La muestran la sala y la pausa.
//   · Joined: a qué se conectó este cliente (lo que la sala y la pausa muestran en un cliente).
// ============================================================
public static class NetAddress
{
    // El puerto de Tugboat por defecto (el transporte de FishNet).
    public const ushort DefaultPort = 7770;

    // A qué se conectó este cliente, ya escrito para mostrar ("192.168.0.5" o "algo.ply.gg:60625").
    // Lo pone la pantalla de conexión al conectarse.
    public static string Joined { get; set; }

    // "host" o "host:puerto". IPv6 no se escribe a mano (tiene ':' adentro): si hay más de un
    // ':', todo es el host.
    public static bool TryParse(string text, out string host, out ushort port)
    {
        host = null;
        port = DefaultPort;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string t = text.Trim();
        int colon = t.LastIndexOf(':');
        if (colon > 0 && t.IndexOf(':') == colon)
        {
            if (!ushort.TryParse(t.Substring(colon + 1), out port) || port == 0) return false;
            t = t.Substring(0, colon).Trim();
        }

        if (t.Length == 0 || t.Contains(" ")) return false;
        host = t;
        return true;
    }

    // Al revés que TryParse: sin el puerto si es el de siempre.
    public static string Format(string host, ushort port) =>
        port == DefaultPort ? host : $"{host}:{port}";

    // =========================================================
    // LA IP DE ESTA PC EN LA RED
    // =========================================================

    private static string _lan;
    private static float  _lanReadAt = -999f;

    // La IPv4 de la placa de red que sale a internet (la que tiene puerta de enlace). Así se
    // saltean las de VirtualBox, Hyper-V y compañía, que también tienen IP pero no llevan a
    // ningún lado. Se relee cada 10 s (cambiar de Wi-Fi la cambia). Sin red: 127.0.0.1.
    public static string LanAddress()
    {
        if (_lan != null && Time.unscaledTime - _lanReadAt < 10f) return _lan;
        _lanReadAt = Time.unscaledTime;

        string withGateway = null, any = null;
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                IPInterfaceProperties props = nic.GetIPProperties();
                bool hasGateway = false;
                foreach (GatewayIPAddressInformation g in props.GatewayAddresses)
                    if (g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))
                        hasGateway = true;

                foreach (UnicastIPAddressInformation u in props.UnicastAddresses)
                {
                    if (u.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = u.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue;   // sin DHCP: no sirve para conectarse

                    if (hasGateway && withGateway == null) withGateway = ip;
                    if (any == null) any = ip;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[NetAddress] No se pudo leer la IP de la red: " + e.Message);
        }

        _lan = withGateway ?? any ?? "127.0.0.1";
        return _lan;
    }
}
