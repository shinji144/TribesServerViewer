using System.Net;

namespace WebViewer.Client.Pages;

public partial class ServerViewer
{
    private List<IPEndPoint> _servers = new();
    private async Task RefreshList()
    {
        this._servers = (await QueryLib.MasterServerClient.GetServersList()).ToList();
    }
}
