using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using TMPro;

public class Player : MonoBehaviour
{
    public List<Card> deck = new();
    public GameObject canvasProfile;

    public string playerName;

    private GameObject cardObject;

    [SerializeField] bool canPlay;
    private int playerSelectedImage;

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
        canvasProfile.transform.Find(profileName).Find(usernameName).GetComponent<TextMeshProUGUI>().text = playerName;
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
        if (Input.GetMouseButtonDown(0) && canPlay)
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);

            if (hit.collider != null)
            {
                if (hit.transform.parent != null && hit.transform.parent.parent != null && hit.transform.parent.parent.gameObject.Equals(gameObject)) // We look for clicks on cards that belong to the player
                {
                    Card clickedCard = hit.collider.GetComponent<Card>();
                    if (GetCardPlayable(clickedCard))
                    {
                        PlayCard(clickedCard);
                    }
                }
                else if (hit.collider.name == "Draw pile") // We look for clicks on the draw pile
                {
                    // We want to draw at least one card - which is why we use a do while loop. 
                    Card drawedCard;
                    do
                    {
                        drawedCard = DrawCard();
                    } while (!GetCardPlayable(drawedCard));
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
    private void PlayCard(Card playedCard)
    {
        gameController.SetTopCard(playedCard);
        FinishTurn(playedCard);
        UpdateDeck();
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
                    deck[i].DestroyCard();
                }
            } 
        }
        //StartCoroutine(WaitForDestroyCardBeforeUpdateDeck());
        gameController.PlayerFinishedTurn(playerIndex);
    }
    private void SkipTurn()
    {
        canPlay = false;
        string playerIndex = gameObject.name.Split(' ').Last();

        lock (deck)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                deck[i].canPlay = false;
            } 
        }
        gameController.PlayerFinishedTurn(playerIndex);
    }
    public bool GetCanPlay() { return canPlay; }

    //private IEnumerator WaitForDestroyCardBeforeUpdateDeck()
    //{
    //    yield return new WaitForEndOfFrame();
    //    UpdateDeck();
    //}
    public List<Card> GetDeck() { return deck; }

    private void DrawHand()
    {
        for (int i = 0; i < 7; i++)
        {
            DrawCard();
        }
    }
    private Card DrawCard()
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

        return instantiatedCard;
    }
    private bool GetCardPlayable(Card card) // Checks if the given card can be played on top of the top card
    {
        return card.GetNumber() == topCard.GetNumber() || card.GetColor() == topCard.GetColor() || card.GetNumber() >= 14;
    }
}
