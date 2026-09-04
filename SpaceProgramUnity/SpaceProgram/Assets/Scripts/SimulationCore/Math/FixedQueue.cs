using System;

struct FixedQueue<T>
{
    readonly T[] items;
    int head;
    int count;

    public FixedQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        items = new T[capacity];
        head = 0;
        count = 0;
    }

    public int Count => count;

    public int Capacity => items.Length;

    public bool IsFull => count >= items.Length;

    public void Enqueue(T item)
    {
        if (IsFull)
        {
            return;
        }

        int tail = (head + count) % items.Length;
        items[tail] = item;
        count++;
    }

    public bool TryDequeue(out T item)
    {
        if (count <= 0)
        {
            item = default;
            return false;
        }

        item = items[head];
        head = (head + 1) % items.Length;
        count--;
        return true;
    }

    public void Clear()
    {
        head = 0;
        count = 0;
    }

    public int DrainTo(T[] destination, int destinationIndex, int maxCount)
    {
        if (destination == null || maxCount <= 0 || count <= 0)
        {
            return 0;
        }

        int drained = 0;

        while (count > 0 && drained < maxCount)
        {
            if (destinationIndex + drained >= destination.Length)
            {
                break;
            }

            destination[destinationIndex + drained] = items[head];
            head = (head + 1) % items.Length;
            count--;
            drained++;
        }

        return drained;
    }
}
