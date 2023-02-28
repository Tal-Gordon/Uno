using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandLayout : MonoBehaviour
{
    public List<GameObject> cards = new List<GameObject>();
    public float yValue = 1f;
    public float spacing = 0.67f;

    private readonly float cardSizeX = 1.289f;
    void Start()
    {
        InvokeRepeating(nameof(UpdateVariables), 0f, 0.25f);
    }

    void Update()
    {
        
    }

    void UpdateVariables()
    {
        cards.Clear();
        int amountOfCards = 0;

        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                amountOfCards++;
                cards.Add(child.gameObject);
            }
        }

        int sortingOrder = 1;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = sortingOrder + i;
            cards[i].transform.localPosition = new Vector3(cards[i].transform.position.x, yValue, 0);
        }
        if (cards.Count > 1)
        {
            UpdateCardsSpacing(spacing);
        }
    }

    private void UpdateCardsSpacing(float spacing)
    {
        int numOfCards = cards.Count;
        int middleCardIndex;
        float cardsMeanXPosition = GetAnchorXPosition();

        if (numOfCards % 2 == 0)
        {
            // Update middle index (left one)
            middleCardIndex = (numOfCards / 2) - 1;

            // Check if middle cards requires repositioning
            if (cards[middleCardIndex].transform.localPosition.x - cards[middleCardIndex + 1].transform.localPosition.x != spacing)
            {
                cards[middleCardIndex].transform.localPosition = new Vector3(cardsMeanXPosition - (spacing / 2), cards[middleCardIndex].transform.localPosition.y, 0);
                cards[middleCardIndex + 1].transform.localPosition = new Vector3(cardsMeanXPosition + spacing / 2, cards[middleCardIndex + 1].transform.localPosition.y, 0);
            }

            // Handle second half of deck
            for (int i = middleCardIndex + 1; i < cards.Count - 1; i++)
            {
                cards[i + 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x + spacing, cards[i + 1].transform.localPosition.y, 0);
            }

            // Handle first half of deck
            for (int i = middleCardIndex; i > 0; i--)
            {
                cards[i - 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x - spacing, cards[i - 1].transform.localPosition.y, 0);
            }
        }

        else if (numOfCards % 2 != 0)
        {
            // Update middle index
            middleCardIndex = numOfCards / 2;

            // Check if middle card requires repositioning
            if (cards[middleCardIndex].transform.localPosition.x != cardsMeanXPosition)
            {
                cards[middleCardIndex].transform.localPosition = new Vector3(cardsMeanXPosition, cards[middleCardIndex].transform.localPosition.y, 0);
            }

            // Handle second half of deck
            for (int i = middleCardIndex; i < cards.Count - 1; i++)
            {
                cards[i + 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x + spacing, cards[i + 1].transform.localPosition.y, 0);
            }

            // Handle first half of deck
            for (int i = middleCardIndex; i > 0; i--)
            {
                cards[i - 1].transform.localPosition = new Vector3(cards[i].transform.localPosition.x - spacing, cards[i + 1].transform.localPosition.y, 0);
            }
        }
    }
    private float GetAnchorXPosition()
    {
        return transform.parent.position.x - (cardSizeX / 2);
    }
}
