import * as Speech from "expo-speech";

/**
 * ADR-005: TTS đi qua interface — implementation mặc định on-device (expo-speech).
 * CapCut/FPT.AI cắm sau bằng cách thêm implementation mới, không đổi chỗ gọi.
 */
export interface ITtsEngine {
  speak(text: string): Promise<void>;
  stop(): Promise<void>;
}

export class OnDeviceTts implements ITtsEngine {
  async speak(text: string): Promise<void> {
    Speech.speak(text, { language: "vi-VN", rate: 1.0 });
  }

  async stop(): Promise<void> {
    await Speech.stop();
  }
}

export const tts: ITtsEngine = new OnDeviceTts();

/** MOB-05: "Bàn số 5 có đơn mới: hai cà phê sữa, một trà đào ít đường." */
export function orderAnnouncement(
  tableCode: string,
  items: { productName: string; quantity: number }[],
): string {
  const itemText = items
    .map((item) => `${item.quantity} ${item.productName}`)
    .join(", ");
  return `Bàn ${tableCode} có đơn mới: ${itemText}.`;
}
