#pragma once
#ifndef __CLARITY_TL_H__
#define __CLARITY_TL_H__

#include <algorithm>

#include "ClarityTypes.h"
#include "ClarityInternalSupport.h"

namespace CLRCore
{
	struct IObjectManager;
}

namespace CLRExec
{
	class Frame;
}

namespace ClarityInternal
{
	template<class TA, class TB>
	struct TAreEqualFunc;

	typedef ::CLRTypes::U32 HashValue;
	typedef ::CLRTypes::SizeT HashEntryIndex;

	template<class TKey, class TItem>
	class HashCollectionBase
	{
	public:
		explicit HashCollectionBase(::CLRCore::IObjectManager *objm);

		void Insert(const ::CLRExec::Frame &frame, HashValue hashValue, const TItem &item);
		void Remove(const ::CLRExec::Frame &frame, HashEntryIndex pos);

		void Rehash(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT newSize);

	private:
		struct Entry
		{
			HashValue hashValue;
			HashEntryIndex next;
			HashEntryIndex prev;
			TItem item;
		};

		template<class TCandidateKey>
		Entry *FindEntry(const TCandidateKey &candidate, HashValue hashValue,
			bool (*compareFunc)(const TCandidateKey &candidate, const TKey &key));

		void PutInFreeList(::CLRTypes::SizeT pos);
		void RemoveFromFreeList(::CLRTypes::SizeT pos);
		void LinkAfter(::CLRTypes::SizeT entryPos, ::CLRTypes::SizeT afterEntryPos);

		Entry *MainPosition(HashValue hashValue);
		Entry *EntryAtPos(::CLRTypes::SizeT pos);
		::CLRTypes::SizeT PosForEntry(Entry *entry);

		static const HashValue INVALID_HASH_VALUE = 0;

		::CLRCore::IObjectManager *m_objectManager;
		Entry *m_entries;

		::CLRTypes::SizeT m_maxEntries;
		::CLRTypes::SizeT m_usedEntries;
		::CLRTypes::SizeT m_nextFree;

		::CLRTypes::SizeT m_invalidIndex;
	};
}


template<class TKey, class TValue>
::ClarityInternal::HashCollectionBase<TKey, TValue>::HashCollectionBase(::CLRCore::IObjectManager *objm)
	: m_objectManager(CLARITY_NULLPTR)
	, m_entries(CLARITY_NULLPTR)
	, m_maxEntries(0)
	, m_usedEntries(0)
	, m_invalidIndex(0)
	, m_nextFree(0)
{
}

template<class TKey, class TItem>
inline void ::ClarityInternal::HashCollectionBase<TKey, TItem>::Insert(const ::CLRExec::Frame &frame,
	::ClarityInternal::HashValue hashValue, const TItem &item)
{
	if (m_maxEntries == m_usedEntries)
		Rehash(m_maxEntries * 2);

	Entry *mainEntry = MainPosition(hashValue);
	HashEntryIndex mainIndex = PosForEntry(mainEntry);
	if (mainEntry->hashValue == INVALID_HASH_VALUE)
	{
		// Inserting into unused location
		HashEntryIndex index = PosForEntry(mainEntry);
		RemoveFromFreeList(index);

		mainEntry->prev = mainEntry->next = index;
		mainEntry->hashValue = hashValue;
		mainEntry->item = item;
	}
	else
	{
		// Collision
		HashEntryIndex freeIndex = PosForEntry(m_nextFree);
		RemoveFromFreeList(freeIndex);

		Entry *freeEntry = EntryAtPos(freeIndex);
		Entry *collidingMainPos = MainPosition(mainEntry->hashValue);

		Entry *insertionPos;
		if (collidingMainPos == mainEntry)
		{
			// Colliding location is in main position, alloc in free
			insertionPos = freeEntry;
		}
		else
		{
			// Colliding location is not in main position, move it to free and alloc in main
			freeEntry->hashValue = mainEntry->hashValue;
			freeEntry->item = mainEntry->item;
			insertionPos = mainEntry;
		}
		insertionPos->hashValue = hashValue;
		insertionPos->item = item;;
		LinkAfter(freeIndex, mainIndex);
	}
}

template<class TKey, class TItem>
inline void ::ClarityInternal::HashCollectionBase<TKey, TItem>::Remove(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT pos)
{
	Entry *toDelete = EntryAtPos(pos);
	Entry *mainPos = MainPosition(toDelete->hashValue);
	HashEntryIndex evictItem;

	if (toDelete == mainPos)
	{
		// Deleting an item in its main position
		if (toDelete->next == toDelete->prev)
		{
			// Nothing chained
			evictItem = pos;
		}
		else
		{
			// Something is chained, move the next
			evictItem = toDelete->next;
			Entry *oldNext = EntryAtPos(evictItem);
			toDelete->hashValue = oldNext->hashValue;
			toDelete->item = oldNext->item;
		}
	}
	else
	{
		// Deleting an item not in its main position
		evictItem = pos;
	}

	PutInFreeList(evictItem);
}

template<class TKey, class TItem>
inline void ::ClarityInternal::HashCollectionBase<TKey, TItem>::Rehash(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT newSize)
{
	if (newSize < 8)
		newSize = 8;

	::CLRTypes::SizeT oldSize = m_maxEntries;
	Entry *oldEntries = m_entries;

	Entry *newEntries = frame.GetObjectManager()->MemAlloc(frame, newSize * sizeof(Entry), false);

	this->m_entries = newEntries;
	this->m_invalidIndex = newSize;
	this->m_maxEntries = newSize;
	this->m_nextFree = 0;
	this->m_usedEntries = 0;

	for (::CLRTypes::SizeT i = 1; i < newSize; i++)
	{
		newEntries[i - 1].next = static_cast<HashEntryIndex>(i);
		newEntries[i].prev = static_cast<HashEntryIndex>(i - 1);
	}

	newEntries[0].prev = newEntries[newSize - 1].next = newSize;

	for (::CLRTypes::SizeT i = 0; i < oldSize; i++)
	{
		const Entry *oldEntry = oldEntries + i;
		Insert(frame, oldEntry->hashValue, oldEntry->item);
	}

	if (oldEntries != CLARITY_NULLPTR)
		frame.GetObjectManager()->MemFree(oldEntries);
}


template<class TKey, class TItem>
template<class TCandidateKey>
inline typename ::ClarityInternal::HashCollectionBase<TKey, TItem>::Entry *CLARITY_MSVC_ROOT_HACK_START ClarityInternal::HashCollectionBase<TKey, TItem>::FindEntry CLARITY_MSVC_ROOT_HACK_END
	(const TCandidateKey &candidate, HashValue hashValue,
	bool(*compareFunc)(const TCandidateKey &candidate, const TKey &key))
{
	Entry *mainPos = this->MainPosition(hashValue);
	if (mainPos->hashValue == INVALID_HASH_VALUE)
		return CLARITY_NULLPTR;

	Entry *scan = mainPos;
	for (;;)
	{
		if (scan->hashValue == hashValue && compareFunc(candidate, scan->item.key))
			return scan;
		scan = EntryAtPos(scan->next);
		if (scan == mainPos)
			return CLARITY_NULLPTR;
	}
}


template<class TKey, class TItem>
inline void ::ClarityInternal::HashCollectionBase<TKey, TItem>::PutInFreeList(::CLRTypes::SizeT pos)
{
	Entry *entry = EntryAtPos(pos);
	entry->hashValue = INVALID_HASH_VALUE;

	if (m_nextFree == m_invalidIndex)
	{
		entry->next = entry->prev = pos;
		m_nextFree = pos;
	}
	else
		LinkAfter(pos, m_nextFree);

	m_usedEntries--;
}

template<class TKey, class TItem>
void ::ClarityInternal::HashCollectionBase<TKey, TItem>::RemoveFromFreeList(::CLRTypes::SizeT pos)
{
	Entry *entry = EntryAtPos(pos);

	if (m_nextFree == pos)
	{
		m_nextFree = entry->next;
		if (m_nextFree == pos)
			m_nextFree = m_invalidIndex;
	}

	Entry *prev = EntryAtPos(entry->prev);
	Entry *next = EntryAtPos(entry->next);

	prev->next = entry->next;
	next->prev = entry->prev;

	m_usedEntries++;
}

template<class TKey, class TItem>
void ::ClarityInternal::HashCollectionBase<TKey, TItem>::LinkAfter(::CLRTypes::SizeT entryPos, ::CLRTypes::SizeT afterEntryPos)
{
	Entry *entry = EntryAtPos(entryPos);
	Entry *prev = EntryAtPos(afterEntryPos);
	Entry *next = EntryAtPos(prev->next);

	entry->next = prev->next;
	entry->prev = next->prev;

	prev->next = next->prev = entryPos;
}

template<class TKey, class TItem>
inline typename ::ClarityInternal::HashCollectionBase<TKey, TItem>::Entry *::ClarityInternal::HashCollectionBase<TKey, TItem>::MainPosition(HashValue hashValue)
{
	return m_entries + (static_cast<HashEntryIndex>(hashValue) % m_maxEntries);
}

template<class TKey, class TItem>
CLARITY_FORCEINLINE typename ::ClarityInternal::HashCollectionBase<TKey, TItem>::Entry *::ClarityInternal::HashCollectionBase<TKey, TItem>::EntryAtPos(::CLRTypes::SizeT pos)
{
	return m_entries + pos;
}

template<class TKey, class TItem>
CLARITY_FORCEINLINE ::CLRTypes::SizeT (::ClarityInternal::HashCollectionBase<TKey, TItem>::PosForEntry)(Entry *entry)
{
	return static_cast< ::CLRTypes::SizeT >(entry - m_entries);
}

#endif
