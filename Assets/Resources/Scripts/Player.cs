using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class Player : MonoBehaviour
{
    public List<Card> deck = new();

    public bool canPlay;

    [SerializeField] GameController gameController;
    private Card topCard;

    private bool unoed;
    private readonly string cardsName = "Cards";
    private readonly string gameControllerName = "Board";
    void Start()
    {
        gameController = GameObject.Find(gameControllerName).GetComponent<GameController>();
        UpdateDeck();
    }

    void UpdateDeck()
    {
        var cards = transform.Find(cardsName);
        deck.Clear();
        for (int i = 0; i < cards.childCount; i++)
        {
            deck.Add(cards.GetChild(i).GetComponent<Card>());
        }
    }

    public void GetTurnAndCheckCards()
    {
        canPlay = true;
        topCard = gameController.GetTopCard();
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i].GetNumber() == topCard.GetNumber() || deck[i].GetColor() == topCard.GetColor() || deck[i].GetNumber() >= 14)
            {
                deck[i].canPlay = true;
            }
        }
    }

    public void FinishTurn(Card playedCard)
    {
        canPlay = false;
        string playerIndex = gameObject.name.Split(' ').Last();

        for (int i = 0; i < deck.Count; i++)
        {
            deck[i].canPlay = false;
            if (deck[i].Equals(playedCard)) 
            { 
                deck[i].DestroyCard(); 
            }
        }
        StartCoroutine(WaitForDestroyCardBeforeUpdateDeck());
        gameController.PlayerFinishedTurn(playerIndex);
    }

    private IEnumerator WaitForDestroyCardBeforeUpdateDeck()
    {
        yield return new WaitForEndOfFrame();
        UpdateDeck();
    }
}
