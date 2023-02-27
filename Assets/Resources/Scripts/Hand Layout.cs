using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandLayout : MonoBehaviour
{
    public GameObject[] cards;
    public float yValue = 1f;
    public float spacing = 1.5f;
    void Start()
    {
        cards = new GameObject[transform.childCount];
        for (int i = 0; i < cards.Length; i++)
        {
            //cards[i] = transform.GetChild(i).GetComponent<Card>();
            cards[i] = transform.GetChild(i).gameObject;
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i + 1;
        }
        InvokeRepeating(nameof(UpdateVariables), 0f, 0.25f);
    }

    void Update()
    {
        
    }

    void UpdateVariables()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].transform.localPosition = new Vector3(cards[i].transform.position.x, yValue, 0);
        }
    }

    private void UpdateCardsSpacing()
    {
        int numOfCards = cards.Length;
        float cardSize = cards[0].GetComponent<SpriteRenderer>().size.x;
        float totalWidth = (cards[cards.GetUpperBound(0)].transform.localPosition.x + cardSize / 2) - (cards[0].transform.localPosition.x - cardSize / 2);
        float leftmostCardXPosition = cards[0].transform.localPosition.x;

        for (int i = 0; i < cards.Length; i++)
        {
            
        }
    }
    private Vector3 GetCardsMeanPosition()
    {
        float xPosition = 0f;
        float yPosition = 0f;

        for (int i = 0; i < cards.Length; i++)
        {
            xPosition += cards[i].transform.localPosition.x;
            yPosition += cards[i].transform.localPosition.y;
        }

        xPosition /= cards.Length;
        yPosition /= cards.Length;

        return new Vector3(xPosition, yPosition, 0);
    }
}
