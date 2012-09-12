using System;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;


public class HyperwallKinectClientSocket
{

    private static string serverIP = "hyperwall02.sv.cmu.edu";         
    private static int serverPort = 8888;

    TcpClient client = null;
    NetworkStream networkStream = null;
    StreamWriter writer = null;
    StreamReader reader = null;

	/* Hyperwall command protocols */
    private static string STATUS_START_AIRMOUSE = "STATUS START_AIRMOUSE";
	private static string STATUS_STOP_AIRMOUSE = "STATUS STOP_AIRMOUSE";
    private static string ANDROID_ACCEL_DATA = "ANDROID_ACCEL_DATA";
	private static string STATUS_CONNECT = "STATUS CONNECT";
	private static string STATUS_DISCONNECT = "STATUS DISCONNECT";
	private static string MOUSE_CLICK = "MOUSE_CLICK";
	private static string ZOOM_IN = "ZOOM_IN";
	private static string ZOOM_OUT = "ZOOM_OUT";
    private static string KINECT_MOVE = "KINECT_MOVE";

    public void connect() 
    {
        client = new TcpClient();   
        Console.WriteLine("Connecting to Hyperwall Proxy Server...");

        // use the ipaddress as in the server program
        try
        {
            client.Connect(serverIP, serverPort);
            networkStream = client.GetStream();
            writer = new StreamWriter(networkStream);
            reader = new StreamReader(networkStream);

            Console.WriteLine("Sending: STATUS_CONNECT");
            sendSingleCommand(STATUS_CONNECT);
            getServerReply();

 
        }
        catch (SocketException e)
        {
            Console.WriteLine("Unable to connect to server");
            Console.WriteLine(e.ToString());
            return;
        }

        Console.WriteLine("Connected");
    }

    private void close()
    {
        if (client.Connected)
        {
            try
            {
                sendSingleCommand(STATUS_DISCONNECT);
                // close connection
                Console.WriteLine("closing TCP connection");
                client.Close();
            }
            catch (IOException e)
            {
                Console.WriteLine("Error when closing connection: " + e.StackTrace);
            }
        }
        else
        {
            Console.WriteLine("Not connected");
        }
    }

    private TcpClient getClient()
    {
        return client;
    }

    public string getServerReply()
    {
        string line = "";
        try
        {
            line = reader.ReadLine();
            System.Console.WriteLine("Received: " + line);
        }
        catch (IOException e)
        {
            Console.WriteLine("Error reading reply message from server: " + e.StackTrace);
        }

        return line;
    }

    private void sendSingleCommand(string command)
    {
        
        if (client.Connected)
        {
            writer.WriteLine(command);
            writer.Flush();
            Console.WriteLine("Sending command: " + command);
        }
        else
        {
            Console.WriteLine("Not connected");
        }
        
    }

    public void zoomIn()
    {
        sendSingleCommand(ZOOM_IN);
    }

    public void zoomOut()
    {
        sendSingleCommand(ZOOM_OUT);
    }

    public void stopAirmouse()
    {
        sendSingleCommand(ANDROID_ACCEL_DATA + " 0 0 0");
    }

    public void kinectMove(double x_ratio, double y_ratio) {
        sendSingleCommand(KINECT_MOVE + " " + x_ratio.ToString() + " " + y_ratio.ToString());
    }

    public void kinectClick() {
        sendSingleCommand(MOUSE_CLICK);
    }

    public void airMouse(double angle, double distance)
    {
        /* Faisal: doing some rough conversion from angle to x,y coordinate movement for proof of concept
         * We need to revisit/fine tune this later
         * Currently we are implementing this here to avoid changes/destabilizing proxy server*/

        int pixel_x, pixel_y;
        int unit = 10;
        
        //determine speed based on the distance
        if (distance < 200)
            unit = 10;
        else if (distance > 200)
            unit = 20;

        double radians = (angle / (double)180)*Math.PI;
        pixel_x =(int)(Math.Cos(radians) * (double)unit);
        pixel_y = (int)(Math.Sin(radians) * (double)unit);
        pixel_x = -pixel_x;
        //Console.WriteLine(radians + " " + Math.Cos(radians) + " " + Math.Sin(radians));
        //Console.WriteLine(radians + " " + pixel_x + " " + pixel_y);
        //Console.WriteLine("pixel_x" + pixel_x);
        //Console.WriteLine("pixel_y" + pixel_y);
        sendSingleCommand(ANDROID_ACCEL_DATA + " " + pixel_x.ToString() + " " + pixel_y.ToString() + " 0");
        /*
        if(angle > 0 && angle <= 90.0)
        {
            sendSingleCommand(ANDROID_ACCEL_DATA + " -5 5 0"); // +x, -y inverted values 
        }
        else if (angle > 90.0 && angle <= 180.0)
        {
            sendSingleCommand(ANDROID_ACCEL_DATA + " -5 -5 0");  // +x, +y inverted values 
        }
        else if (angle > 180.0 && angle <= 270.0)
        {
            sendSingleCommand(ANDROID_ACCEL_DATA + " 5 -5 0");  // -x, +y inverted values 
        }
        else if (angle > 270.0 && angle <= 360.0)
        {
            sendSingleCommand(ANDROID_ACCEL_DATA + " 5 5 0");  // -x, -y inverted values 
        }
        */

    }
    /*
    public static void Main()
    {
        HyperwallKinectClientSocket kinectSocket = new HyperwallKinectClientSocket();


        try
        {
            kinectSocket.connect();
            kinectSocket.sendSingleCommand(MOUSE_CLICK);
            kinectSocket.sendSingleCommand(ZOOM_IN);
            kinectSocket.sendSingleCommand(ZOOM_OUT);

            // testing airmouse 
            kinectSocket.sendSingleCommand(STATUS_START_AIRMOUSE);
            kinectSocket.airMouse((float)110.0, 2);
            Thread.Sleep(5000);
            kinectSocket.sendSingleCommand(ANDROID_ACCEL_DATA + " 0 0 0"); 
     
            kinectSocket.sendSingleCommand(STATUS_STOP_AIRMOUSE);
            
            Console.ReadLine(); // break to see output
            kinectSocket.close();
            Console.ReadLine(); // break to see output
        }

        catch (Exception e)
        {
            Console.WriteLine("Houston, we have a problem: " + e.StackTrace);
        }
    }
    */
}
