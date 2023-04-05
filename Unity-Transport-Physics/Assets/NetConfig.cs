using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetConfig: MonoBehaviour
{
    public delegate void NetShutdownAction();
    public static event NetShutdownAction OnNetShutdown;

    [SerializeField]static string ipAddress = "127.0.0.1";
    [SerializeField]static int port = 8888;

    bool serverStarted = false;
    bool clientStarted = false;

    InputField ipField;
    InputField portField;
    Button StartServerButton;
    Button StartClientButton;
    Button DisconnectButton;
    Slider TickRateSlider;
    Text ModeText;

    void Start()
    {
        Application.runInBackground = true;
        ipField = GameObject.Find("IPField").GetComponent<InputField>();
        portField = GameObject.Find("PortField").GetComponent<InputField>();
        StartServerButton = GameObject.Find("StartServerButton").GetComponent<Button>();
        StartClientButton = GameObject.Find("StartClientButton").GetComponent<Button>();
        DisconnectButton = GameObject.Find("DisconnectButton").GetComponent<Button>();
        TickRateSlider = GameObject.Find("TickRateSlider").GetComponent<Slider>();
        TickRateSlider.gameObject.SetActive(false);
        ModeText = GameObject.Find("ModeText").GetComponent<Text>();
    }

    public void SetIP()
    {
        Debug.Log("IP UPDATE");
        ipAddress = ipField.text;
    }

    public void SetPort()
    {
        port = int.Parse(portField.text);
    }

    public static string GetIP()
    {
        return ipAddress;
    }

    public static int GetPort()
    {
        return port;
    }

    public void StartAsServer()
    {
        gameObject.AddComponent<Server>();
        if(GetComponent<Server>().StartServer())
        {
            serverStarted = true;
            TickRateSlider.gameObject.SetActive(true);
            ElementsActiveControl(false, false, true, true, false, false);
            TickRateSlider.value = GetComponent<Server>().TickRate;
            TickRateSlider.GetComponentInChildren<TMP_Text>().text = TickRateSlider.value.ToString();
            ModeText.text = "Mode: Server";         
        }
        else
        {
            Debug.LogError("Unable to start the server!");
        }
    }

    public void StartAsClient()
    {
        gameObject.AddComponent<Client>();

        if (GetComponent<Client>().ConnectToServer())
        {
            clientStarted = true;
            ElementsActiveControl(false, false, true, false, false, false);
            ModeText.text = "Mode: Client";
        }
        else
        {
            Debug.LogError("Unable to start the client!");
        }
    }

    public void Disconnect()
    {
        if(serverStarted)
        {
            GetComponent<Server>().ShutDown();
            TickRateSlider.gameObject.SetActive(false);
            serverStarted = false;
        }
        if(clientStarted)
        {
            GetComponent<Client>().Disconnect();
            clientStarted = false;
        }

        ElementsActiveControl(true, true, true, false, true, true);
        ModeText.text = "Mode: Nothing";
    }

    public void ChangeTickrate(float value)
    {
        if(GetComponent<Server>())
        {
            GetComponent<Server>().TickRate = value;
            TickRateSlider.GetComponentInChildren<TMP_Text>().text = value.ToString();
        }
    }

    void ElementsActiveControl(bool serverButton, bool clientButton, bool disconnectButton, bool tickRateSlider, bool ipTextBox, bool portTextBox)
    {
        StartServerButton.interactable = serverButton;
        StartClientButton.interactable = clientButton;
        DisconnectButton.interactable = disconnectButton;
        TickRateSlider.interactable = tickRateSlider;
        ipField.interactable = ipTextBox;
        portField.interactable = portTextBox;
    }

    void OnEnabled()
    {
        OnNetShutdown += Disconnect;
    }

    void OnDisabled()
    {
        OnNetShutdown -= Disconnect;
    }
}
