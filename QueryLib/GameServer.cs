using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace QueryLib
{
    public record class GameServer(IPEndPoint ServerAddress) : IDisposable
    {
        public string? Name { get; set; }
        public double? Ping { get; set; }
        public string? MissionType { get; set; }
        public string? MissionName { get; set; }
        public byte Players { get; set; }
        public byte MaxPlayers { get; set; }
        public string PlayersFmt => $"{this.Players}/{this.MaxPlayers}";
        public string? Address => this.ServerAddress.ToString();
        public string? Version { get; set; }
        public string? GameInfo { get; set; }
        public bool Dedicated { get; set; }
        public bool Passworded { get; set; }
        public short CpuSpeed { get; set; }

        //public required IPEndPoint ServerAddress { get; init; }

        public string? Mod { get; set; }

        private UdpClient? _udpClient = null;

        public void Refresh()
        {
            Debug.WriteLine("Refresh()");
            if (this._udpClient == null)
                this._udpClient = new UdpClient(this.ServerAddress.Address.ToString(), this.ServerAddress.Port);

            try
            {
                var timeStart = DateTime.Now;

                // Request server info

                byte[] request = [0x62, 0x01, 0x07];
                this._udpClient.Send(request, request.Length);
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] response = this._udpClient.Receive(ref remoteEndPoint);
                int offset = 0;
                Debug.WriteLine("got server response");
                this.Ping = (DateTime.Now - timeStart).TotalMilliseconds; //calc this ourselves from the udp connection speed

                this.GetInt32(response, ref offset); //skip header
                this.GetPascalString(response, ref offset); //skip game

                this.Version = this.GetPascalString(response, ref offset);
                this.Name = this.GetPascalString(response, ref offset);
                this.Dedicated = this.GetByte(response, ref offset) == 1;
                this.Passworded = this.GetByte(response, ref offset) == 1;
                this.Players = this.GetByte(response, ref offset);
                this.MaxPlayers = this.GetByte(response, ref offset);
                this.CpuSpeed = this.GetInt16(response, ref offset);
                this.Mod = this.GetPascalString(response, ref offset);
                this.MissionType = this.GetPascalString(response, ref offset);
                this.MissionName = this.GetPascalString(response, ref offset);
                //this.GameInfo = this.GetPascalString(response, ref offset);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                this._udpClient?.Close();
            }

            Debug.WriteLine("finished response");
        }
        public async Task RefreshAsync()
        {
            Debug.WriteLine("RefreshAsync()");
            if (this._udpClient == null)
                this._udpClient = new UdpClient(this.ServerAddress.Address.ToString(), this.ServerAddress.Port);
            
            await Task.Run(() =>
            {
                try
                {
                    var timeStart = DateTime.Now;

                    // Request server info

                    byte[] request = [0x62, 0x01, 0x02];
                    this._udpClient.Send(request, request.Length);
                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] response = this._udpClient.Receive(ref remoteEndPoint);
                    int offset = 0;
                    Debug.WriteLine("got server response");
                    this.Ping = (DateTime.Now - timeStart).TotalMilliseconds; //calc this ourselves from the udp connection speed

                    this.GetInt32(response, ref offset); //skip header
                    this.GetPascalString(response, ref offset); //skip game

                    this.Version = this.GetPascalString(response, ref offset);
                    this.Name = this.GetPascalString(response, ref offset);
                    this.Dedicated = this.GetByte(response, ref offset) == 1;
                    this.Passworded = this.GetByte(response, ref offset) == 1;
                    this.Players = this.GetByte(response, ref offset);
                    this.MaxPlayers = this.GetByte(response, ref offset);
                    this.CpuSpeed = this.GetInt16(response, ref offset);
                    this.Mod = this.GetPascalString(response, ref offset);
                    this.MissionType = this.GetPascalString(response, ref offset);
                    this.MissionName = this.GetPascalString(response, ref offset);
                    //this.GameInfo = this.GetPascalString(response, ref offset);
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    this._udpClient?.Close();
                }
                
                Debug.WriteLine("finished response");
            });
        }

        #region byte conversion methods

        private byte GetByte(byte[] buffer, ref int offset)
        {
            return buffer[offset++];
        }

        private short GetInt16(byte[] buffer, ref int offset)
        {
            short result = BitConverter.ToInt16(buffer, offset);
            offset += 2;
            return result;
        }

        private int GetInt32(byte[] buffer, ref int offset)
        {
            int result = BitConverter.ToInt32(buffer, offset);
            offset += 4;
            return result;
        }

        private string GetPascalString(byte[] buffer, ref int offset)
        {
            int len = this.GetByte(buffer, ref offset);
            if (len > 0)
            {
                string result = Encoding.ASCII.GetString(buffer, offset, len);
                offset += len;
                return result;
            }
            return "";
        }

        #endregion byte conversion methods

        public void Dispose()
        {
            this._udpClient?.Dispose();
        }
    }
}