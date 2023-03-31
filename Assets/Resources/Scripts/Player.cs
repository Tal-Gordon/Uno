using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using TMPro;
using System.Runtime.CompilerServices;
using UnityEngine.UI;
using System.Drawing;

public class Player : MonoBehaviour
{
    public List<Card> deck = new();
    public GameObject canvas;

    public string playerName;

    private GameObject cardObject;

    [SerializeField] bool canPlay;

    private GameController gameController;
    private Card topCard;

    private bool unoed;
    private readonly string cardsName = "Cards";
    private readonly string gameControllerName = "Board";
    private readonly string profileName = "Profile";
    private readonly string usernameName = "Username";
    void Start()
    {
        gameController = GameObject.Find(gameControllerName).GetComponent<GameController>();
        canvas.transform.Find(gameObject.name).Find(profileName).Find(usernameName).GetComponent<TextMeshProUGUI>().text = playerName;
        cardObject = Resources.Load<GameObject>("Prefabs/Card");
        DrawHand();
    }

    void UpdateDeck()
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
    private void Play()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);

            if (hit.collider != null)
            {
                if (hit.transform.parent != null && hit.transform.parent.parent != null && hit.transform.parent.parent.gameObject.Equals(gameObject)) // We look for clicks on cards that belong to the player
                {
                    Card clickedCard = hit.collider.GetComponent<Card>();
                    if (GetCardPlayable(clickedCard))
                    {
                        FinishTurn(clickedCard);
                    }
                }
                else if (hit.collider.name == "Draw pile")
                {
                    Card drawedCard;
                    if (gameController.drawToMatch)
                    {
                        // We want to draw at least one card
                        do
                        {
                            drawedCard = DrawCard();
                        } while (!GetCardPlayable(drawedCard)); 
                    }
                    else { drawedCard = DrawCard(); }

                    //if (gameController.forcePlay) { FinishTurn(drawedCard); }
                    //else { SkipTurn(); }

                    // TODO
                    SkipTurn();
                }
            }
        }
    }
    public void GetTurnAndCheckCards()
    {
        canPlay = true;
        topCard = gameController.GetTopCard();
        for (int i = 0; i < deck.Count; i++)
        {
            if (GetCardPlayable(deck[i]))
            {
                deck[i].canPlay = true;
            }
        }
    }
    public void FinishTurn(Card playedCard)
    {
        canPlay = false;
        string playerIndex = gameObject.name.Split(' ').Last();

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
        gameController.PlayerFinishedTurn(gameObject.GetComponent<Player>(), playedCard);
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

    private void DrawHand()
    {
        for (int i = 0; i < 7; i++)
        {
            DrawCard();
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

        Invoke(nameof(UpdateCardsLayout), 0.1f);

        return instantiatedCard;
    }
    private Card DrawCard(Card card)
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

        Invoke(nameof(UpdateCardsLayout), 0.1f);

        return instantiatedCard;
    }
    private bool GetCardPlayable(Card card) // Checks if the given card can be played on top of the top card
    {
        return card.GetNumber() == topCard.GetNumber() || card.GetColor() == topCard.GetColor() || card.GetNumber() >= 14;
    }
    public void WildColoring()
    {
        GameObject coloring = canvas.transform.Find("Wild coloring").gameObject;
        coloring.SetActive(true);
        for (int i = 0; i < coloring.transform.childCount; i++)
        {
            coloring.transform.GetChild(i).GetComponent<Button>().onClick.AddListener(MutateColor);
        }
    }
    public void Draw4Coloring()
    {
        GameObject coloring = canvas.transform.Find("Draw4 coloring").gameObject;
        coloring.SetActive(true);
        for (int i = 0; i < coloring.transform.childCount; i++)
        {
            coloring.transform.GetChild(i).GetComponent<Button>().onClick.AddListener(MutateColor);
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
    public void ChangeHands()
    {
        GameObject arrows = canvas.transform.Find("Seven arrows").gameObject;
        arrows.SetActive(true);
        for (int i = 0; i < arrows.transform.childCount; i++)
        {
            arrows.transform.GetChild(i).GetComponent<Button>().onClick.AddListener(PickPlayer);
            switch (gameController.GetTopCard().GetColor())
            {
                case CardColor.Green:
                {
                    arrows.transform.GetChild(i).GetComponent<Image>().color = new Color32(48, 247, 71, 255);
                    break;
                }
                case CardColor.Yellow:
                {
                    arrows.transform.GetChild(i).GetComponent<Image>().color = new Color32(230, 249, 45, 255);
                    break;
                }
                case CardColor.Red:
                {
                    arrows.transform.GetChild(i).GetComponent<Image>().color = new Color32(236, 39, 23, 255);
                    break;
                }
                case CardColor.Blue:
                {
                    arrows.transform.GetChild(i).GetComponent<Image>().color = new Color32(47, 248, 246, 255);
                    break;
                }
            }
        }
    }
    public void PickPlayer()
    {
        GameObject player = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        string pickedPlayerIndex = player.name.Split(' ').Last();

        gameController.SwapHands(gameObject.name.Split(' ').Last(), pickedPlayerIndex);

        GameObject arrows = canvas.transform.Find("Seven arrows").gameObject;
        arrows.SetActive(false);
    }
    private void UpdateCardsLayout()
    {
        transform.GetChild(0).GetComponent<HandLayout>().UpdateVariables();
    }
    public void ShowUnoButton()
    {
        GameObject unoButton = canvas.transform.Find("Uno").gameObject;
        unoButton.SetActive(true);
        unoButton.GetComponent<Button>().onClick.AddListener(CallUno);
    }
    public void CallUno()
    {
        unoed = true;
        canvas.transform.Find("Uno").gameObject.SetActive(false);
    }
    public void ShowCallOutButton()
    {
        GameObject unoButton = canvas.transform.Find("Call out").gameObject;
        unoButton.SetActive(true);
        unoButton.GetComponent<Button>().onClick.AddListener(CallOutUnunoed);
    }
    public void CallOutUnunoed()
    {
        canvas.transform.Find("Call out").gameObject.SetActive(false);
    }
}