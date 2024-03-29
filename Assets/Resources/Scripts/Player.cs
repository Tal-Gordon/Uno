using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour, IPlayer
{
    public List<Card> deck = new();

    public string playerName;
    public bool aiDriven;

    private GameObject canvas;
    private GameObject cardObject;
    private GameController gameController;
    private AIplayer playerAI;
    private Server server;
    private Client client;

    [SerializeField] private bool canPlay;
    private bool unoed;
    private bool stackPotential = false;
    private readonly string cardsName = "Cards";
    private void Awake()
    {
        cardObject = Resources.Load<GameObject>("Prefabs/Card");
    }

    private void Start()
    {
        server = Server.Instance; client = Client.Instance;

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root.name == "Board") { gameController = root.GetComponent<GameController>(); }
            if (root.name == "Canvas") { canvas = root; }
        }
        canvas.transform.Find(gameObject.name).Find("Profile").Find("Username").GetComponent<TextMeshProUGUI>().text = playerName;

        if (aiDriven)
        {
            playerAI = gameObject.AddComponent<AIplayer>();
            playerAI.SetPlayer(GetComponent<Player>());
        }
    }
    public void StartNewGame()
    {
        UnshowMatchEndPanel();
        DrawHand();
    }

    private void UpdateDeck()
    {
        var cards = transform.Find(cardsName);
        lock (deck)
        {
            deck.Clear();

            for (int i = 0; i < deck.Count; i++)
            {
                deck[i] = cards.GetChild(i).GetComponent<Card>();
            }
        }
    }
    private void Update()
    {
        if (canPlay)
        {
            Play();
        }
    }
    public void Play()
    {
        if (!aiDriven)
        {
            if (Input.GetMouseButtonDown(0))
            {
                RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);

                if (hit.collider != null)
                {
                    if (hit.transform.parent != null && hit.transform.parent.parent != null && hit.transform.parent.parent.gameObject.Equals(gameObject)) // We look for clicks only on cards that belong to the player
                    {
                        Card clickedCard = hit.collider.GetComponent<Card>();
                        if (GetCardPlayable(clickedCard))
                        {
                            gameController.PlayerFinishedTurn(this, clickedCard);
                        }
                    }
                    else if (hit.collider.name == "Draw pile")
                    {
                        DrawCardsLogic();
                    }
                }
            }
        }
        else
        {
            playerAI.DoPlay();
        }
    }
    public void DrawCardsLogic()
    {
        if (server.active)
        {
            DrawCard();
            server.DrawedCard(GetIndex());
        }
        else if (client.active)
        {
            client.DrawCardFromServer();
        }
        SkipTurn();
    }
    public void GetTurnAndCheckCards()
    {
        canPlay = true;
        for (int i = 0; i < deck.Count; i++)
        {
            if (GetCardPlayable(deck[i]))
            {
                deck[i].canPlay = true;
            }
        }
        if (GetCanUno()) { ShowUnoButton(); }
    }
    public void GetTurnAndCheckForStacking(Card card)
    {
        canPlay = true;
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i].GetNumber() == card.GetNumber())
            {
                deck[i].canPlay = true;
                stackPotential = true;
            }
        }
        if (GetCanUno()) { ShowUnoButton(); }
    }
    private bool GetCanUno()
    {
        int counter = 0;
        for (int i = 0; i < deck.Count; i++)
        {
            if (GetCardPlayable(deck[i]))
            {
                counter++;
            }
        }
        return counter > 0 && deck.Count == 2;
    }
    public void FinishTurn(Card playedCard)
    {
        canPlay = false;

        if (playedCard != null)
        {
            lock (deck)
            {
                for (int i = 0; i < deck.Count; i++)
                {
                    deck[i].canPlay = false;
                    if (deck[i].Equals(playedCard))
                    {
                        deck.RemoveAt(i);
                        playedCard.DestroyCard();
                        Invoke(nameof(UpdateCardsLayout), 0.1f);
                    }
                }
            } 
        }
    }
    private void SkipTurn()
    {
        canPlay = false;

        lock (deck)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                deck[i].canPlay = false;
            }
        }
        gameController.PlayerFinishedTurn(gameObject.GetComponent<Player>(), null);
    }
    public void CheckForJumpIn()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            if (GetCardPlayable(deck[i]))
            {
                // TODO
            }
        }
    }
    public bool GetCanPlay() { return canPlay; }
    public List<Card> GetDeck() { return deck; }
    public void SetDeck(List<Card> newDeck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            deck[i].DestroyCard();
        }
        deck.Clear();

        for (int i = 0; i < newDeck.Count; i++)
        {
            DrawCard(newDeck[i]);
        }
    }
    public void SetDeck(string deckRepresentation)
    {
        string[] parts = deckRepresentation.Split(";");
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length < 3)
            {
                Debug.LogError($"Invalid input string at index {i}: {parts[i]}");
                continue; // skip this input string and move on to the next one
            }

            if (!int.TryParse(parts[i].Substring(0, 2), out int num))
            {
                Debug.LogError($"Invalid number at index {i}: {parts[i]}");
                continue; // skip this input string and move on to the next one
            }

            if (!Enum.TryParse(parts[i].Substring(2), out CardColor color))
            {
                Debug.LogError($"Invalid color at index {i}: {parts[i]}");
                continue; // skip this input string and move on to the next one
            }

            DrawCard(num, color);
        }
    }
    private void DrawHand()
    {
        ClearHand();
        if (server.active)
        {
            for (int i = 0; i < 7; i++)
            {
                DrawCard();
            }
        }
        else if (client.active)
        {
            client.DrawHand();
        }
    }
    public Card DrawCard()
    {
        Card instantiatedCard = Instantiate(cardObject, transform.Find(cardsName)).GetComponent<Card>();
        lock (deck)
        {
            deck.Add(instantiatedCard);
        }

        // Get card "values" (num and color) and set card object properties to drawn card
        (int, CardColor) drawedCard = gameController.DrawCard();
        instantiatedCard.SetNumber(drawedCard.Item1);
        instantiatedCard.SetColor(drawedCard.Item2);

        // Set name from "Card(Clone)" to meaningful name that represents card properties
        instantiatedCard.gameObject.name = $"{instantiatedCard.GetNumber()} {instantiatedCard.GetColor()}";

        instantiatedCard.handCard = true;
        if (GetComponent<AIplayer>()) 
        { 
            instantiatedCard.hidden = true; 
            instantiatedCard.handCard = false; 
        }

        Invoke(nameof(UpdateCardsLayout), 0.1f);

        return instantiatedCard;
    }
    public Card DrawCard(Card card)
    {
        Card instantiatedCard = Instantiate(cardObject, transform.Find(cardsName)).GetComponent<Card>();
        lock (deck)
        {
            deck.Add(instantiatedCard);
        }

        instantiatedCard.SetNumber(card.GetNumber());
        instantiatedCard.SetColor(card.GetColor());

        // Set name from "Card(Clone)" to meaningful name that represents card properties
        instantiatedCard.gameObject.name = $"{instantiatedCard.GetNumber()} {instantiatedCard.GetColor()}";

        instantiatedCard.handCard = true;
        if (aiDriven) { instantiatedCard.hidden = true; }

        instantiatedCard.UpdateTexture();
        Invoke(nameof(UpdateCardsLayout), 0.1f);

        return instantiatedCard;
    }
    public Card DrawCard(int num, CardColor color)
    {
        Card instantiatedCard = Instantiate(cardObject, transform.Find(cardsName)).GetComponent<Card>();
        lock (deck)
        {
            deck.Add(instantiatedCard);
        }

        instantiatedCard.SetNumber(num);
        instantiatedCard.SetColor(color);

        // Set name from "Card(Clone)" to meaningful name that represents card properties
        instantiatedCard.gameObject.name = $"{instantiatedCard.GetNumber()} {instantiatedCard.GetColor()}";

        instantiatedCard.handCard = true;
        if (aiDriven) { instantiatedCard.hidden = true; }

        instantiatedCard.UpdateTexture();
        Invoke(nameof(UpdateCardsLayout), 0.1f);

        return instantiatedCard;
    }
    private void ClearHand()
    {
        transform.GetChild(0).GetComponent<HandLayout>().ClearDeck();
        deck.Clear();
    }
    public bool GetCardPlayable(Card card) // Checks if the given card can be played on top of the top card
    {
        bool hasColorOfTopCard = false;
        if (card.GetNumber() == 15 && gameController.noBluffing)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i].GetColor() == card.GetColor()) { hasColorOfTopCard = true; break; }
            }
        }
        return card.GetNumber() == gameController.GetTopCard().GetNumber() || card.GetColor() == gameController.GetTopCard().GetColor() || card.GetNumber() == 14 ||
            (card.GetNumber() == 15 && ((gameController.noBluffing && !hasColorOfTopCard) || (!gameController.noBluffing)));
    }
    public void WildColoring()
    {
        if (!aiDriven)
        {
            GameObject coloring = canvas.transform.Find("Wild coloring").gameObject;
            coloring.SetActive(true);
            for (int i = 0; i < coloring.transform.childCount; i++)
            {
                coloring.transform.GetChild(i).GetComponent<Button>().onClick.AddListener(MutateColor);
            }
        }
        else
        {
            Card toMutate = gameController.GetTopCard();

            Enum.TryParse(playerAI.GetActionToDo("wildcolor"), out CardColor color);
            toMutate.SetColor(color);

            gameController.PlayerFinishedTurn(gameObject.GetComponent<Player>(), toMutate);
            FinishColoring();
        }
    }
    public void Draw4Coloring()
    {
        if (!aiDriven)
        {
            GameObject coloring = canvas.transform.Find("Draw4 coloring").gameObject;
            coloring.SetActive(true);
            for (int i = 0; i < coloring.transform.childCount; i++)
            {
                coloring.transform.GetChild(i).GetComponent<Button>().onClick.AddListener(MutateColor);
            }
        }
        else
        {
            Card toMutate = gameController.GetTopCard();

            Enum.TryParse(playerAI.GetActionToDo("draw4color"), out CardColor color);
            toMutate.SetColor(color);

            gameController.PlayerFinishedTurn(gameObject.GetComponent<Player>(), toMutate);
            FinishColoring();
        }
    }
    private void FinishColoring()
    {
        GameObject draw4 = canvas.transform.Find("Draw4 coloring").gameObject;
        draw4.SetActive(false);
        GameObject wild = canvas.transform.Find("Wild coloring").gameObject;
        wild.SetActive(false);
    }
    public void MutateColor()
    {
        GameObject color = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        Card toMutate = gameController.GetTopCard();

        switch (color.name.Trim())
        {
            case "Green":
            {
                //toMutate.MutateColor(CardColor.Green);
                toMutate.SetColor(CardColor.Green);
                break;
            }
            case "Yellow":
            {
                //toMutate.MutateColor(CardColor.Yellow);
                toMutate.SetColor(CardColor.Yellow);
                break;
            }
            case "Red":
            {
                //toMutate.MutateColor(CardColor.Red);
                toMutate.SetColor(CardColor.Red);
                break;
            }
            case "Blue":
            {
                //toMutate.MutateColor(CardColor.Blue);
                toMutate.SetColor(CardColor.Blue);
                break;
            }
        }
        gameController.PlayerFinishedTurn(gameObject.GetComponent<Player>(), toMutate);
        FinishColoring();
    }
    private void UpdateCardsLayout()
    {
        transform.GetChild(0).GetComponent<HandLayout>().UpdateVariables();
    }
    private void ShowUnoButton()
    {
        if (!aiDriven)
        {
            GameObject unoButton = canvas.transform.Find("Uno").gameObject;
            unoButton.SetActive(true);
            unoButton.GetComponent<Button>().onClick.AddListener(CallUno);
        }
        else
        {
            if (playerAI.GetActionToDo("uno") == "uno") { CallUno(); }
        }
    }
    public void UnshowUnoButton()
    {
        canvas.transform.Find("Uno").gameObject.SetActive(false);
    }
    private void ShowChallengeButton()
    {
        if (!aiDriven)
        {
            GameObject unoButton = canvas.transform.Find("Challenge").gameObject;
            unoButton.SetActive(true);
            unoButton.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(Challenge);
            unoButton.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(DeclineChallenge);
        }
        else
        {
            if (playerAI.GetActionToDo("challenge") == "challenge") { Challenge(); }
            else { DeclineChallenge(); }
        }
    }
    public void UnshowChallengeButtons()
    {
        canvas.transform.Find("Challenge").gameObject.SetActive(false);
    }
    public void ShowCallOutButton()
    {
        GameObject unoButton = canvas.transform.Find("Call out").gameObject;
        unoButton.SetActive(true);
        unoButton.GetComponent<Button>().onClick.AddListener(CallOutUnunoed);
    }
    public void UnshowCallOutButton()
    {
        canvas.transform.Find("Call out").gameObject.SetActive(false);
    }
    public void ShowKeepPlayButtons(Card drawedCard)
    {
        if (!aiDriven)
        {
            GameObject buttons = canvas.transform.Find("Keep or play").gameObject;
            buttons.SetActive(true);

            buttons.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(KeepButton);
            buttons.transform.GetChild(1).GetComponent<Button>().onClick.AddListener(delegate { PlayButton(drawedCard); }); 
        }
        else
        {
            if (playerAI.GetActionToDo("keeporplay") == "keep") { KeepButton(); }
            else { PlayButton(drawedCard); }
        }
    }
    private void UnshowKeepPlayButtons()
    {
        canvas.transform.Find("Keep or play").gameObject.SetActive(false);
    }
    public void ShowMatchEndPanelClient()
    {
        GameObject matchEndObject = canvas.transform.Find("Match end client").gameObject;
        matchEndObject.SetActive(true);

        matchEndObject.transform.GetChild(0).GetChild(1).GetComponent<Button>().onClick.AddListener(client.DisconnectFromServer);
    }
    public void ShowMatchEndPanelServer()
    {
        GameObject matchEndObject = canvas.transform.Find("Match end server").gameObject;
        matchEndObject.SetActive(true);

        matchEndObject.transform.GetChild(0).GetChild(1).GetChild(0).GetComponent<Button>().onClick.AddListener(server.CloseServer);
        matchEndObject.transform.GetChild(0).GetChild(1).GetChild(1).GetComponent<Button>().onClick.AddListener(delegate { StartCoroutine(gameController.LateStart(0.5f)); });
    }
    private void UnshowMatchEndPanel()
    {
        canvas.transform.Find("Match end server").gameObject.SetActive(false);
        canvas.transform.Find("Match end client").gameObject.SetActive(false);
    }
    public void CallUno()
    {
        unoed = true;
        canvas.transform.Find("Uno").gameObject.SetActive(false);
        if (client.active)
        {
            client.CallUno();
        }
        else if (server.active)
        {
            server.CallUno();
        }
    }
    public void CallOutUnunoed()
    {
        UnshowCallOutButton();
        gameController.CalledOutUnunoed(GetIndex());
    }
    public void KeepButton()
    {
        SkipTurn();
        UnshowKeepPlayButtons();
    }
    public void PlayButton(Card cardToPlay)
    {
        FinishTurn(cardToPlay);
        UnshowKeepPlayButtons();
    }
    public void Challenge()
    {
        UnshowChallengeButtons();
    }
    public void DeclineChallenge()
    {

        UnshowChallengeButtons();
    }
    public bool GetUnoed() { return unoed; }
    public string GetIndex() { return gameObject.name.Split(' ').Last(); }
    public void SetGameobjectName(string name) { gameObject.name = name; }
    public bool GetStackPotential() { return stackPotential; }
}