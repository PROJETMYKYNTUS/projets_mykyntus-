/** Draft question partagé (templates catalogue + session quiz). */
export type QuizDraftQuestion = {
  id?: string;
  type: 'Qcm' | 'FreeText';
  prompt: string;
  options: string[];
  correctOptionIndex: number;
  correctOptionIndexes: number[];
  allowMultiple: boolean;
  points: number;
  imageUrl: string;
  explanation: string;
  /** image | video — utile pour l’aperçu après upload. */
  mediaKind?: 'image' | 'video' | null;
};

export function emptyQuizDraftQuestion(): QuizDraftQuestion {
  return {
    type: 'Qcm',
    prompt: '',
    options: ['', ''],
    correctOptionIndex: 0,
    correctOptionIndexes: [0],
    allowMultiple: false,
    points: 1,
    imageUrl: '',
    explanation: '',
    mediaKind: null,
  };
}

export function isQuizMediaVideo(url: string | null | undefined, mediaKind?: string | null): boolean {
  if (mediaKind === 'video') return true;
  if (mediaKind === 'image') return false;
  const u = (url ?? '').toLowerCase();
  return /\.(mp4|webm|ogg|mov)(\?|#|$)/i.test(u);
}

export function buildQuizQuestionPayload(questions: QuizDraftQuestion[]) {
  return questions
    .map((q) => {
      const prompt = q.prompt.trim();
      if (!prompt) return null;
      if (q.type === 'FreeText') {
        return {
          id: q.id || null,
          type: 1,
          prompt,
          options: null as string[] | null,
          correctOptionIndex: null as number | null,
          correctOptionIndexes: null as number[] | null,
          allowMultiple: false,
          points: q.points || 1,
          imageUrl: q.imageUrl.trim() || null,
          explanation: q.explanation.trim() || null,
        };
      }
      const options = q.options.map((o) => o.trim()).filter(Boolean);
      if (options.length < 2) return null;
      const indexes = q.allowMultiple
        ? [...q.correctOptionIndexes].filter((i) => i >= 0 && i < options.length)
        : [Math.min(q.correctOptionIndex, options.length - 1)];
      const safe = indexes.length ? indexes : [0];
      return {
        id: q.id || null,
        type: 0,
        prompt,
        options,
        correctOptionIndex: safe[0],
        correctOptionIndexes: safe,
        allowMultiple: q.allowMultiple,
        points: q.points || 1,
        imageUrl: q.imageUrl.trim() || null,
        explanation: q.explanation.trim() || null,
      };
    })
    .filter((x): x is NonNullable<typeof x> => !!x);
}
