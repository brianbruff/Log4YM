#!/usr/bin/env python3
"""Import ADIF log file into MongoDB Atlas."""

import re
from pymongo import MongoClient
from datetime import datetime

def parse_adif(file_path):
    """Parse an ADIF file and yield records as dictionaries."""
    with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
        content = f.read()

    # Find where header ends (after <EOH>)
    eoh_match = re.search(r'<EOH>', content, re.IGNORECASE)
    if eoh_match:
        content = content[eoh_match.end():]

    # Split by end of record marker
    records = re.split(r'<EOR>', content, flags=re.IGNORECASE)

    # Pattern to match ADIF fields: <fieldname:length>value or <fieldname:length:type>value
    field_pattern = re.compile(r'<(\w+):(\d+)(?::\w)?>(.*?)(?=<|\Z)', re.DOTALL)

    for record in records:
        record = record.strip()
        if not record:
            continue

        doc = {}
        for match in field_pattern.finditer(record):
            field_name = match.group(1).lower()
            length = int(match.group(2))
            value = match.group(3)[:length].strip()

            if value:
                # Convert numeric fields
                if field_name in ('freq', 'freq_rx', 'tx_pwr', 'distance', 'cqz', 'ituz', 'dxcc',
                                  'my_cq_zone', 'my_dxcc', 'my_itu_zone'):
                    try:
                        if '.' in value:
                            value = float(value)
                        else:
                            value = int(value)
                    except ValueError:
                        pass

                # Parse date fields into proper datetime
                if field_name == 'qso_date' and len(value) == 8:
                    try:
                        doc['qso_datetime'] = datetime.strptime(value, '%Y%m%d')
                    except ValueError:
                        pass

                # Combine date and time for qso_datetime
                if field_name == 'time_on' and 'qso_date' in doc:
                    try:
                        date_str = doc.get('qso_date', '')
                        if len(date_str) == 8 and len(value) >= 4:
                            time_str = value.ljust(6, '0')[:6]
                            doc['qso_datetime'] = datetime.strptime(f"{date_str}{time_str}", '%Y%m%d%H%M%S')
                    except ValueError:
                        pass

                doc[field_name] = value

        if doc:
            # Add import metadata
            doc['imported_at'] = datetime.utcnow()
            yield doc

def main():
    # MongoDB connection
    connection_string = "todo: your MongoDB connection string here"

    client = MongoClient(connection_string)
    db = client['Log4YM']
    collection = db['qso']

    # Path to ADIF file
    adif_file = '/Users/brian.keating/Downloads/log.xml'

    print(f"Parsing ADIF file: {adif_file}")

    records = list(parse_adif(adif_file))
    total_records = len(records)

    print(f"Found {total_records} QSO records")

    if total_records > 0:
        # Insert in batches for efficiency
        batch_size = 100
        inserted = 0

        for i in range(0, total_records, batch_size):
            batch = records[i:i + batch_size]
            result = collection.insert_many(batch)
            inserted += len(result.inserted_ids)
            print(f"Inserted {inserted}/{total_records} records...")

        print(f"\nSuccessfully imported {inserted} QSO records into Log4YM.qso collection")
    else:
        print("No records found to import")

    client.close()

if __name__ == '__main__':
    main()
