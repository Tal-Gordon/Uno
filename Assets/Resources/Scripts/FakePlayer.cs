using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

public class FakePlayer : MonoBehaviour, IPlayer
{
    public List<Card> deck = new();
    public GameObject canvas;

    public string playerName;

    private GameObject cardObject;

    private void Awake()
    {
        cardObject = Resources.Load<GameObject>("Prefabs/Card");
    }

    private void Start()
    {
        canvas.transform.Find(gameObject.name).Find("Profile").Find("Username").GetComponent<TextMeshProUGUI>().text = playerName;
    }

    public Card DrawCard()
    {
        Card instantiatedCard = Instantiate(cardObject, transform.Find("Cards")).GetComponent<Card>();
        lock (deck)
        {
            deck.Add(instantiatedCard);
        }
        // In need of checking
        Invoke(nameof(UpdateCardsLayout), 0.1f);

        return instantiatedCard;
    }
    public void FinishTurn(Card card)
    {
        deck[Random.Range(0, deck.Count - 1)].GetComponent<Card>().DestroyCard();
    }
    public void StartNewGame()
    {
        for (int i = 0; i < 7; i++)
        {
            DrawCard();
        }
    }
    public string GetIndex() { return name.Split(' ').Last(); }
    public void SetGameobjectName(string name) { gameObject.name = name; }
    private void UpdateCardsLayout()
    {
        transform.GetChild(0).GetComponent<HandLayout>().UpdateVariables();
    }
    public void ChangeHands()
    {

    }
    public List<Card> GetDeck() { return deck; }
    public void SetDeck(List<Card> newDeck) 
    {
        int deckDifference = newDeck.Count - GetDeck().Count;
        if (deckDifference > 0)
        {
            for (int i = 0; i < deckDifference; i++)
            {
                DrawCard();
            }
        }
        else if (deckDifference < 0)
        {
            for (int i = 0; i < -deckDifference; i++)
            {
                deck[i].DestroyCard();
            }
        }
    }
}
