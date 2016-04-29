using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WebuSocket {
	public static class WebSocketByteGenerator {
		// #0                   1                   2                   3
		// #0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
		// #+-+-+-+-+-------+-+-------------+-------------------------------+
		// #|F|R|R|R| opcode|M| Payload len |    Extended payload length    |
		// #|I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
		// #|N|V|V|V|       |S|             |   (if payload len==126/127)   |
		// #| |1|2|3|       |K|             |                               |
		// #+-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
		// #|     Extended payload length continued, if payload len == 127  |
		// #+ - - - - - - - - - - - - - - - +-------------------------------+
		// #|                               | Masking-key, if MASK set to 1 |
		// #+-------------------------------+-------------------------------+
		// #| Masking-key (continued)       |          Payload Data         |
		// #+-------------------------------- - - - - - - - - - - - - - - - +
		// #:                     Payload Data continued ...                :
		// #+ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
		// #|                     Payload Data continued ...                |

		public const byte OP_CONTINUATION	= 0x0; //unsupported.
		public const byte OP_TEXT			= 0x1;// 0001 unsupported.
		public const byte OP_BINARY			= 0x2;// 0010
		public const byte OP_CLOSE			= 0x8;// 1000
		public const byte OP_PING			= 0x9;// 1001
		public const byte OP_PONG			= 0xA;// 1010
		
		private const byte OPFilter			= 0xF;// 1111
		private const byte Length7Filter	= 0xBF;// 01111111
		
		public static byte[] Ping () {
			return WSDataFrame(1, 0, 0, 0, OP_PING, 1, new byte[0]);
		}
		
		public static byte[] Pong () {
			return WSDataFrame(1, 0, 0, 0, OP_PONG, 1, new byte[0]);
		}
		
		public static byte[] SendBinaryData (byte[] data) {
			return WSDataFrame(1, 0, 0, 0, OP_BINARY, 1, data);
		}
				
		public static byte[] CloseData () {
			return WSDataFrame(1, 0, 0, 0, OP_CLOSE, 1, new byte[0]);
		}
		
		private static byte[] WSDataFrame (
			byte fin,
			byte rsv1,
			byte rsv2,
			byte rsv3,
			byte opCode,
			byte mask,
			byte[] data)
		{
			uint length = (uint)(data.Length);
			
			byte dataLength7bit = 0;
			UInt16 dataLength16bit = 0;
			uint dataLength64bit = 0;
			
			if (length < 126) {
				dataLength7bit = (byte)length;
			} else if (length < 65536) {
				dataLength7bit = 126;
				dataLength16bit = (UInt16)length;
			} else {
				dataLength7bit = 127;
				dataLength64bit = length;
			}
			
			/*
				ready data stream structure for send.
			*/
			using (var dataStream = new MemoryStream()) { 
				dataStream.WriteByte((byte)((fin << 7) | (rsv1 << 6) | (rsv2 << 5) | (rsv3 << 4) | opCode));
				dataStream.WriteByte((byte)((mask << 7) | dataLength7bit));
				
				if (0 < dataLength16bit) {
					var intBytes = new byte[2];
					intBytes[0] = (byte)(dataLength16bit >> 8);
					intBytes[1] = (byte)dataLength16bit;
					
					// dataLength16 to 2bytes.
					dataStream.Write(intBytes, 0, intBytes.Length);
				}
				if (0 < dataLength64bit) {
					var intBytes = new byte[8];
					intBytes[0] = (byte)(dataLength64bit >> (8*7));
					intBytes[1] = (byte)(dataLength64bit >> (8*6));
					intBytes[2] = (byte)(dataLength64bit >> (8*5));
					intBytes[3] = (byte)(dataLength64bit >> (8*4));
					intBytes[4] = (byte)(dataLength64bit >> (8*3));
					intBytes[5] = (byte)(dataLength64bit >> (8*2));
					intBytes[6] = (byte)(dataLength64bit >> 8);
					intBytes[7] = (byte)dataLength64bit;
					
					// dataLength64 to 8bytes.
					dataStream.Write(intBytes, 0, intBytes.Length);
				}
				
				// client should mask control frame.
				var maskKey = WebuSocketClient.NewMaskKey();
				dataStream.Write(maskKey, 0, maskKey.Length);
				
				// mask data.
				var maskedData = data.Masked(maskKey);
				dataStream.Write(maskedData, 0, maskedData.Length);
				
				return dataStream.ToArray();
			}
		}
		
		private static byte[] Masked (this byte[] data, byte[] maskKey) {
			for (var i = 0; i < data.Length; i++) data[i] ^= maskKey[i%4];
			return data;
		}
		
		/**
			get message detail from data.
			no copy emitted. only read data then return there indexies of messages.
		*/
		public static List<OpCodeAndPayloadIndex> GetIndexies (byte[] data) {
			var opCodeAndPayloadIndexies = new List<OpCodeAndPayloadIndex>();
			
			uint messageHead;
			uint cursor = 0;
			while (cursor < data.Length) {
				messageHead = cursor;
				
				// first byte = fin(1), rsv1(1), rsv2(1), rsv3(1), opCode(4)
				var opCode = (byte)(data[cursor++] & OPFilter);
				
				// second byte = mask(1), length(7)
				/*
					mask of data from server is definitely zero(0).
					ignore reading mask bit.
				*/
				uint length = (uint)data[cursor++];
				switch (length) {
					case 126: {
						// next 2 byte is length data.
						length = (uint)(
							(data[cursor++] << 8) +
							(data[cursor++])
						);
						break;
					}
					case 127: {
						// next 8 byte is length data.
						length = (uint)(
							(data[cursor++] << (8*7)) +
							(data[cursor++] << (8*6)) +
							(data[cursor++] << (8*5)) +
							(data[cursor++] << (8*4)) +
							(data[cursor++] << (8*3)) +
							(data[cursor++] << (8*2)) +
							(data[cursor++] << 8) +
							(data[cursor++])
						);
						break;
					}
					default: {
						break;
					}
				}
				
				/*
					shortage of payload length.
					the whole payload datas of this message is not yet read from socket.
					
					break indexing then store the rest = header of fragment data and half of payload.
				*/
				if ((data.Length - cursor) < length) break;
				
				if (length != 0) {
					var payload = new byte[length];
					Array.Copy(data, cursor, payload, 0, payload.Length);
				}
				
				opCodeAndPayloadIndexies.Add(new OpCodeAndPayloadIndex(opCode, cursor, length));
				
				cursor = cursor + length; 
			}
			
			return opCodeAndPayloadIndexies;
		}
		
		public struct OpCodeAndPayloadIndex {
			public readonly byte opCode;
			public readonly uint start;
			public readonly uint length;
			public OpCodeAndPayloadIndex (byte opCode, uint start, uint length) {
				this.opCode = opCode;
				this.start = start;
				this.length = length;
			}
		}
		
		public static byte[] SubArray (this byte[] data, uint index, uint length) {
    		var result = new byte[length];
    		Array.Copy(data, index, result, 0, length);
    		return result;
		}
	}
}