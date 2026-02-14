import { useState, useEffect, useRef } from 'react';
import { Bot, Send, RefreshCw, Settings, Loader2, Key, Calendar, MessageCircleQuestion } from 'lucide-react';
import Markdown from 'react-markdown';
import { useAppStore } from '../store/appStore';
import { useSettingsStore } from '../store/settingsStore';
import { GlassPanel } from '../components/GlassPanel';
import { api, type GenerateTalkPointsResponse, type ChatMessage as ChatMessageType } from '../api/client';

const BAND_RANGES: Record<string, [number, number]> = {
  '160m': [1800, 2000], '80m': [3500, 4000], '60m': [5330, 5410],
  '40m': [7000, 7300], '30m': [10100, 10150], '20m': [14000, 14350],
  '17m': [18068, 18168], '15m': [21000, 21450], '12m': [24890, 24990],
  '10m': [28000, 29700], '6m': [50000, 54000],
};

const getBandFromFrequency = (freqHz: number): string | undefined => {
  const freqKhz = freqHz / 1000;
  for (const [band, [min, max]] of Object.entries(BAND_RANGES)) {
    if (freqKhz >= min && freqKhz <= max) return band;
  }
  return undefined;
};

export function ChatAiPlugin() {
  const { focusedCallsignInfo, isLookingUpCallsign, rigStatus } = useAppStore();
  const { settings, openSettings } = useSettingsStore();
  const [isGenerating, setIsGenerating] = useState(false);
  const [talkPointsData, setTalkPointsData] = useState<GenerateTalkPointsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [chatMessages, setChatMessages] = useState<ChatMessageType[]>([]);
  const [chatInput, setChatInput] = useState('');
  const [isSendingChat, setIsSendingChat] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const callsign = focusedCallsignInfo?.callsign;
  const hasApiKey = settings.ai.apiKey && settings.ai.apiKey.length > 0;

  // Auto-generate talk points when callsign changes (if enabled)
  useEffect(() => {
    if (callsign && hasApiKey && settings.ai.autoGenerateTalkPoints) {
      handleGenerate();
    } else if (!callsign) {
      // Clear when no callsign
      setTalkPointsData(null);
      setChatMessages([]);
      setError(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [callsign]);

  // Scroll to bottom when messages change
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [chatMessages]);

  const handleGenerate = async () => {
    if (!callsign) return;

    setIsGenerating(true);
    setError(null);

    try {
      const response = await api.generateTalkPoints({
        callsign,
        currentBand: rigStatus?.frequency ? getBandFromFrequency(rigStatus.frequency) : undefined,
        currentMode: rigStatus?.mode,
      });
      setTalkPointsData(response);
      setChatMessages([]); // Clear chat when regenerating
    } catch (err) {
      console.error('Failed to generate talk points:', err);
      setError('Failed to generate talk points. Check your API key and try again.');
    } finally {
      setIsGenerating(false);
    }
  };

  const handleSendChat = async () => {
    if (!callsign || !chatInput.trim() || isSendingChat) return;

    const question = chatInput.trim();
    setChatInput('');

    // Add user message to chat
    const userMessage: ChatMessageType = { role: 'user', content: question };
    setChatMessages((prev) => [...prev, userMessage]);
    setIsSendingChat(true);
    setError(null);

    try {
      const response = await api.chat({
        callsign,
        question,
        conversationHistory: chatMessages,
      });

      // Add assistant response
      const assistantMessage: ChatMessageType = { role: 'assistant', content: response.answer };
      setChatMessages((prev) => [...prev, assistantMessage]);
    } catch (err) {
      console.error('Failed to send chat:', err);
      setError('Failed to get response. Check your API key and try again.');
    } finally {
      setIsSendingChat(false);
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendChat();
    }
  };

  // Empty state - no API key
  if (!hasApiKey) {
    return (
      <GlassPanel title="Chat AI" icon={<Bot className="w-5 h-5" />}>
        <div className="p-4 h-full flex flex-col items-center justify-center">
          <Key className="w-16 h-16 text-amber-500 mb-4" />
          <p className="text-lg font-medium text-gray-300 mb-2">API Key Required</p>
          <p className="text-sm text-gray-500 text-center mb-6 max-w-md">
            To use Chat AI, add your API key from OpenAI or Anthropic in the settings.
            <br />
            <br />
            Your key is stored locally and never sent to Log4YM servers.
          </p>
          <button
            onClick={openSettings}
            className="glass-button px-4 py-2 flex items-center gap-2 text-accent-primary hover:bg-accent-primary/10"
          >
            <Settings className="w-4 h-4" />
            Open Settings
          </button>
        </div>
      </GlassPanel>
    );
  }

  // Empty state - no callsign
  if (!callsign) {
    return (
      <GlassPanel title="Chat AI" icon={<Bot className="w-5 h-5" />}>
        <div className="p-4 h-full flex flex-col">
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <Bot className="w-16 h-16 text-gray-600 mx-auto mb-4" />
              <p className="text-lg font-medium text-gray-300 mb-2">No Callsign Selected</p>
              <p className="text-sm text-gray-500 max-w-md">
                Enter a callsign in the Log Entry or click a DX spot to get AI-powered talk points and QSO context.
              </p>
            </div>
          </div>
          <div className="border-t border-glass-100 pt-3 mt-3">
            <div className="flex gap-2">
              <input
                type="text"
                placeholder="Ask something..."
                disabled
                className="glass-input flex-1 opacity-50 cursor-not-allowed"
              />
              <button disabled className="glass-button px-4 py-2 opacity-50 cursor-not-allowed">
                <Send className="w-4 h-4" />
              </button>
            </div>
          </div>
        </div>
      </GlassPanel>
    );
  }

  // Loading state
  if (isLookingUpCallsign || (isGenerating && !talkPointsData)) {
    return (
      <GlassPanel title="Chat AI" icon={<Bot className="w-5 h-5" />}>
        <div className="p-4 h-full flex flex-col">
          <div className="glass-panel mb-4 p-3">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-lg font-mono font-bold text-accent-primary">{callsign}</p>
                {focusedCallsignInfo?.name && (
                  <p className="text-sm text-gray-400">{focusedCallsignInfo.name}</p>
                )}
              </div>
            </div>
          </div>
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <Loader2 className="w-12 h-12 text-accent-primary mx-auto mb-4 animate-spin" />
              <p className="text-gray-400 text-sm mb-1">Searching log history...</p>
              <p className="text-gray-400 text-sm mb-1">Reading QRZ profile...</p>
              <p className="text-gray-400 text-sm">Generating talk points...</p>
            </div>
          </div>
        </div>
      </GlassPanel>
    );
  }

  // Error state
  if (error && !talkPointsData) {
    return (
      <GlassPanel title="Chat AI" icon={<Bot className="w-5 h-5" />}>
        <div className="p-4 h-full flex flex-col">
          <div className="glass-panel mb-4 p-3">
            <p className="text-lg font-mono font-bold text-accent-primary">{callsign}</p>
          </div>
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <div className="text-red-400 mb-4">{error}</div>
              <button
                onClick={handleGenerate}
                className="glass-button px-4 py-2 flex items-center gap-2 mx-auto"
              >
                <RefreshCw className="w-4 h-4" />
                Try Again
              </button>
            </div>
          </div>
        </div>
      </GlassPanel>
    );
  }

  // Main view with talk points
  return (
    <GlassPanel
      title="Chat AI"
      icon={<Bot className="w-5 h-5" />}
      actions={
        <button
          onClick={handleGenerate}
          disabled={isGenerating}
          className="glass-button p-1.5 text-xs flex items-center gap-1 disabled:opacity-50"
          title="Refresh talk points"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isGenerating ? 'animate-spin' : ''}`} />
          Refresh
        </button>
      }
    >
      <div className="p-4 h-full flex flex-col">
        {/* Callsign header */}
        <div className="glass-panel mb-4 p-3">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-lg font-mono font-bold text-accent-primary">{callsign}</p>
              {focusedCallsignInfo?.name && (
                <p className="text-sm text-gray-400">{focusedCallsignInfo.name}</p>
              )}
            </div>
            <div className="text-right text-sm text-gray-500">
              {focusedCallsignInfo?.grid && (
                <div>Grid: {focusedCallsignInfo.grid}</div>
              )}
              {focusedCallsignInfo?.distance && (
                <div>{Math.round(focusedCallsignInfo.distance).toLocaleString()} km</div>
              )}
            </div>
          </div>
        </div>

        {/* Scrollable content area */}
        <div className="flex-1 overflow-y-auto space-y-4 mb-4">
          {/* Previous QSOs */}
          {talkPointsData && talkPointsData.previousQsos.length > 0 && (
            <div>
              <h3 className="text-sm font-semibold text-gray-400 mb-2 flex items-center gap-2">
                <Calendar className="w-4 h-4" />
                Previous QSOs ({talkPointsData.previousQsos.length})
              </h3>
              <div className="space-y-2">
                {talkPointsData.previousQsos.map((qso, idx) => (
                  <div key={idx} className="glass-panel p-2 text-sm">
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-gray-400">{new Date(qso.qsoDate).toLocaleDateString()}</span>
                      <span className="text-accent-info font-mono">
                        {qso.band} {qso.mode}
                      </span>
                    </div>
                    {qso.comment && (
                      <p className="text-gray-300 italic">"{qso.comment}"</p>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Talk Points */}
          {talkPointsData && talkPointsData.talkPoints.length > 0 && (
            <div>
              <h3 className="text-sm font-semibold text-gray-400 mb-2 flex items-center gap-2">
                <Bot className="w-4 h-4" />
                Talk Points
              </h3>
              <div className="space-y-2">
                {talkPointsData.talkPoints.map((point, idx) => (
                  <div key={idx} className="glass-panel p-3 text-sm text-gray-300 flex gap-2">
                    <span className="text-accent-primary">💬</span>
                    <span>{point}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Follow-up suggestions */}
          {talkPointsData && talkPointsData.talkPoints.length > 0 && chatMessages.length === 0 && (
            <div className="space-y-1.5">
              <h3 className="text-sm font-semibold text-gray-400 mb-2 flex items-center gap-2">
                <MessageCircleQuestion className="w-4 h-4" />
                Ask a Follow-up
              </h3>
              {[
                `What are the current propagation conditions to ${focusedCallsignInfo?.country || 'their location'}?`,
                `What else can you tell me about ${callsign}'s station and interests?`,
                `Any contest or award tips for working ${focusedCallsignInfo?.country || 'this region'}?`,
              ].map((suggestion, idx) => (
                <button
                  key={idx}
                  onClick={() => {
                    setChatInput(suggestion);
                    // Auto-send after setting input
                    const userMessage: ChatMessageType = { role: 'user', content: suggestion };
                    setChatMessages((prev) => [...prev, userMessage]);
                    setIsSendingChat(true);
                    setError(null);
                    api.chat({ callsign: callsign!, question: suggestion, conversationHistory: chatMessages })
                      .then((response) => {
                        const assistantMessage: ChatMessageType = { role: 'assistant', content: response.answer };
                        setChatMessages((prev) => [...prev, assistantMessage]);
                      })
                      .catch(() => setError('Failed to get response. Check your API key and try again.'))
                      .finally(() => { setIsSendingChat(false); setChatInput(''); });
                  }}
                  className="w-full text-left glass-panel p-2.5 text-sm text-gray-400 hover:text-gray-200 hover:bg-glass-100 transition-colors cursor-pointer flex items-start gap-2"
                >
                  <Send className="w-3.5 h-3.5 mt-0.5 shrink-0 text-accent-primary/60" />
                  <span>{suggestion}</span>
                </button>
              ))}
            </div>
          )}

          {/* Chat messages */}
          {chatMessages.length > 0 && (
            <div>
              <h3 className="text-sm font-semibold text-gray-400 mb-2">Chat</h3>
              <div className="space-y-2">
                {chatMessages.map((msg, idx) => (
                  <div
                    key={idx}
                    className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
                  >
                    <div
                      className={`max-w-[80%] rounded-lg p-3 text-sm ${
                        msg.role === 'user'
                          ? 'bg-accent-primary/20 text-gray-200'
                          : 'glass-panel text-gray-300'
                      }`}
                    >
                      {msg.role === 'assistant' ? (
                        <Markdown
                          components={{
                            h1: ({ children }) => <h1 className="text-lg font-bold text-gray-200 mt-3 mb-1 first:mt-0">{children}</h1>,
                            h2: ({ children }) => <h2 className="text-base font-bold text-gray-200 mt-3 mb-1 first:mt-0">{children}</h2>,
                            h3: ({ children }) => <h3 className="text-sm font-bold text-gray-200 mt-2 mb-1 first:mt-0">{children}</h3>,
                            p: ({ children }) => <p className="mb-2 last:mb-0">{children}</p>,
                            strong: ({ children }) => <strong className="font-semibold text-gray-200">{children}</strong>,
                            em: ({ children }) => <em className="italic text-gray-400">{children}</em>,
                            ul: ({ children }) => <ul className="list-disc list-inside mb-2 space-y-0.5">{children}</ul>,
                            ol: ({ children }) => <ol className="list-decimal list-inside mb-2 space-y-0.5">{children}</ol>,
                            li: ({ children }) => <li className="text-gray-300">{children}</li>,
                            a: ({ href, children }) => <a href={href} target="_blank" rel="noopener noreferrer" className="text-accent-primary hover:underline">{children}</a>,
                            code: ({ children }) => <code className="bg-glass-100 px-1 py-0.5 rounded text-accent-info text-xs">{children}</code>,
                          }}
                        >
                          {msg.content}
                        </Markdown>
                      ) : (
                        msg.content
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Chat input */}
        <div className="border-t border-glass-100 pt-3">
          {error && <div className="text-red-400 text-sm mb-2">{error}</div>}
          <div className="flex gap-2">
            <input
              type="text"
              value={chatInput}
              onChange={(e) => setChatInput(e.target.value)}
              onKeyPress={handleKeyPress}
              placeholder={`Ask something about ${callsign}...`}
              disabled={isSendingChat}
              className="glass-input flex-1"
            />
            <button
              onClick={handleSendChat}
              disabled={!chatInput.trim() || isSendingChat}
              className="glass-button px-4 py-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isSendingChat ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <Send className="w-4 h-4" />
              )}
            </button>
          </div>
        </div>
      </div>
    </GlassPanel>
  );
}
